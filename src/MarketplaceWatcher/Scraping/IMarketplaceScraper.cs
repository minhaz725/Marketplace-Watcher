using MarketplaceWatcher.Data;

namespace MarketplaceWatcher.Scraping;

public interface IMarketplaceScraper
{
    Task<IReadOnlyList<ScrapedCard>> ScrapeAsync(WatchEntity watch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a single listing's own page to read the title the search grid did not render.
    /// Returns null if the page has no title either, or could not be loaded.
    /// Callers MUST cache the result — see <see cref="Data.CheckedListing"/>.
    /// </summary>
    Task<string?> FetchTitleAsync(string listingId, CancellationToken cancellationToken = default);
}
