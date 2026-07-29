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

    private static readonly HarvestPolicy Policy = new(maxRounds: 12, stagnantRoundsBeforeStop: 3);

    private CdpConnection? _connection;

    /// <summary>
    /// Returns a live CDP connection, reconnecting if the cached one has gone stale.
    ///
    /// Connecting spins up a Playwright driver process, so the connection is reused across
    /// polls. But Chrome restarting leaves the cached IBrowser alive with **no contexts**,
    /// and the old `_connection ??=` never noticed — every poll then failed forever until
    /// the app itself was restarted.
    /// </summary>
    private async Task<CdpConnection> GetConnectionAsync()
    {
        if (_connection is { IsUsable: true })
        {
            return _connection;
        }

        if (_connection is not null)
        {
            logger.LogWarning("CDP connection went stale (Chrome closed or restarted); reconnecting");
            await _connection.DisposeAsync();
            _connection = null;
        }

        return _connection = await CdpConnection.ConnectAsync();
    }

    public async Task<IReadOnlyList<ScrapedCard>> ScrapeAsync(WatchEntity watch, CancellationToken cancellationToken = default)
    {
        var cdp = await GetConnectionAsync();
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

    public async Task<string?> FetchTitleAsync(string listingId, CancellationToken cancellationToken = default)
    {
        var cdp = await GetConnectionAsync();
        var page = await cdp.Context.NewPageAsync();
        try
        {
            await page.GotoAsync($"https://www.facebook.com/marketplace/item/{listingId}/");

            // Verified against real listings: the bare "h1" is Facebook's chrome
            // ("Notifications"); the listing title is the h1 inside the main region.
            var title = await page.InnerTextAsync("[role='main'] h1", new() { Timeout = 15_000 });
            return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        }
        catch (Exception ex)
        {
            // A failed enrichment must not abort the poll. It is cached as "no title" by the
            // caller, so a permanently broken listing is not retried forever.
            logger.LogWarning("Could not read title for listing {ListingId}: {Message}", listingId, ex.Message);
            return null;
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
