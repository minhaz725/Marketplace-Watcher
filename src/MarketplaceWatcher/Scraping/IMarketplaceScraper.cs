using MarketplaceWatcher.Data;

namespace MarketplaceWatcher.Scraping;

public interface IMarketplaceScraper
{
    Task<IReadOnlyList<ScrapedCard>> ScrapeAsync(WatchEntity watch, CancellationToken cancellationToken = default);
}
