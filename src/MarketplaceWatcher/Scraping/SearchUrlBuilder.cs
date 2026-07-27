using MarketplaceWatcher.Data;

namespace MarketplaceWatcher.Scraping;

/// <summary>
/// Builds the Marketplace URL for a watch. Pure so the query-string assembly is unit tested
/// rather than only observable by scraping Facebook.
/// </summary>
public static class SearchUrlBuilder
{
    public static string Build(WatchEntity watch, int? daysSinceListed)
    {
        var url = watch.FreeOnly
            ? "https://www.facebook.com/marketplace/category/free"
            : $"https://www.facebook.com/marketplace/search?query={Uri.EscapeDataString(watch.Name ?? string.Empty)}";

        if (daysSinceListed is > 0)
        {
            // Verified against live data: Marketplace honours daysSinceListed (result sets
            // differ far beyond the run-to-run noise floor, and the share of cards tagged
            // "Just listed" rises from ~16% to ~77%). Note it does NOT honour sortBy or
            // radius — see PROGRESS.md. Separator depends on whether a query string exists.
            var separator = url.Contains('?') ? '&' : '?';
            url = $"{url}{separator}daysSinceListed={daysSinceListed}";
        }

        return url;
    }
}
