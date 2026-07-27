using MarketplaceWatcher.Config;
using MarketplaceWatcher.Data;
using Microsoft.Extensions.Options;

namespace MarketplaceWatcher.Scraping;

public sealed class MarketplaceScraper(
    ILogger<MarketplaceScraper> logger,
    IOptions<WatcherOptions> options) : IMarketplaceScraper, IAsyncDisposable
{
    private const string CardSelector = "a[href*='/marketplace/item/']";
    private const int ScrollStepPx = 1600;
    private const int ScrollWaitMs = 1500;
    private const int FirstCardTimeoutMs = 20_000;

    private static readonly HarvestPolicy Policy = new(maxRounds: 12, stagnantRoundsBeforeStop: 2);

    private CdpConnection? _connection;

    public async Task<IReadOnlyList<ScrapedCard>> ScrapeAsync(WatchEntity watch, CancellationToken cancellationToken = default)
    {
        var cdp = _connection ??= await CdpConnection.ConnectAsync();
        var url = SearchUrlBuilder.Build(watch, options.Value.DaysSinceListed);

        var page = await cdp.Context.NewPageAsync();
        try
        {
            logger.LogInformation("Scraping {Url} for watch {Name}", url, watch.Name ?? "(free-only / match-all)");
            await page.GotoAsync(url);

            // Marketplace renders its grid client-side, so GotoAsync returning does NOT mean
            // cards exist yet. Harvesting immediately was the main cause of the Phase 10
            // recon finding where one run saw 23 listings and the next saw 46 for the same URL.
            try
            {
                await page.WaitForSelectorAsync(CardSelector, new() { Timeout = FirstCardTimeoutMs });
            }
            catch (TimeoutException)
            {
                logger.LogWarning("No listing cards appeared within {Timeout}ms for {Url}", FirstCardTimeoutMs, url);
                return [];
            }

            var seenIds = new HashSet<string>();
            var cards = new List<ScrapedCard>();
            var rounds = 0;
            var stagnantRounds = 0;

            // Scroll until the feed stops yielding anything new (or we hit the round cap),
            // rather than a fixed 5 iterations that could stop mid-feed.
            while (Policy.ShouldContinue(rounds, stagnantRounds))
            {
                var ariaLabels = await page.EvalOnSelectorAllAsync<string[]>(
                    CardSelector,
                    "els => els.map(e => e.getAttribute('aria-label'))");

                var foundThisRound = 0;
                foreach (var label in ariaLabels)
                {
                    var card = label is not null ? CardParser.Parse(label) : null;
                    if (card is not null && seenIds.Add(card.ListingId))
                    {
                        cards.Add(card);
                        foundThisRound++;
                    }
                }

                stagnantRounds = foundThisRound == 0 ? stagnantRounds + 1 : 0;
                rounds++;

                await page.Mouse.WheelAsync(0, ScrollStepPx);
                await page.WaitForTimeoutAsync(ScrollWaitMs);
            }

            logger.LogInformation(
                "Harvested {Count} listings in {Rounds} scroll round(s) for watch {Name}",
                cards.Count, rounds, watch.Name ?? "(free-only / match-all)");

            if (Policy.StoppedEarly(rounds, stagnantRounds))
            {
                logger.LogWarning(
                    "Harvest for watch {Name} hit the {Rounds}-round cap while still finding new "
                    + "listings — the feed was NOT exhausted and listings were missed.",
                    watch.Name ?? "(free-only / match-all)", rounds);
            }

            return cards;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
