using MarketplaceWatcher.Data;

namespace MarketplaceWatcher.Scraping;

public sealed class MarketplaceScraper(ILogger<MarketplaceScraper> logger) : IMarketplaceScraper, IAsyncDisposable
{
    private const int MaxScrolls = 5;
    private const int ScrollStepPx = 1600;
    private const int ScrollWaitMs = 1500;

    private CdpConnection? _connection;

    public async Task<IReadOnlyList<ScrapedCard>> ScrapeAsync(WatchEntity watch, CancellationToken cancellationToken = default)
    {
        var cdp = _connection ??= await CdpConnection.ConnectAsync();
        var url = BuildSearchUrl(watch);

        var page = await cdp.Context.NewPageAsync();
        try
        {
            logger.LogInformation("Scraping {Url} for watch {Name}", url, watch.Name ?? "(free-only / match-all)");
            await page.GotoAsync(url);

            var seenIds = new HashSet<string>();
            var cards = new List<ScrapedCard>();

            for (var i = 0; i < MaxScrolls; i++)
            {
                var ariaLabels = await page.EvalOnSelectorAllAsync<string[]>(
                    "a[href*='/marketplace/item/']",
                    "els => els.map(e => e.getAttribute('aria-label'))");

                foreach (var label in ariaLabels)
                {
                    var card = label is not null ? CardParser.Parse(label) : null;
                    if (card is not null && seenIds.Add(card.ListingId))
                    {
                        cards.Add(card);
                    }
                }

                await page.Mouse.WheelAsync(0, ScrollStepPx);
                await page.WaitForTimeoutAsync(ScrollWaitMs);
            }

            return cards;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static string BuildSearchUrl(WatchEntity watch) =>
        watch.FreeOnly
            ? "https://www.facebook.com/marketplace/category/free"
            : $"https://www.facebook.com/marketplace/search?query={Uri.EscapeDataString(watch.Name ?? string.Empty)}";

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
