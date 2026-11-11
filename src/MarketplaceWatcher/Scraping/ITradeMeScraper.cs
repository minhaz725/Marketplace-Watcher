using MarketplaceWatcher.Data;

namespace MarketplaceWatcher.Scraping;

public interface ITradeMeScraper
{
    /// <summary>First page of Trade Me Motors results for the watch, newest first.</summary>
    Task<IReadOnlyList<TradeMeCard>> ScrapeAsync(WatchEntity watch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens one listing's page to read registration and WOF expiry. Callers MUST cache the
    /// result (see <see cref="CheckedListing"/>) so each listing is opened at most once.
    /// </summary>
    Task<(DateTime? Rego, DateTime? Wof)> FetchExpiryAsync(string listingUrl, CancellationToken cancellationToken = default);
}
