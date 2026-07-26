namespace MarketplaceWatcher.Scraping;

/// <summary>
/// Keeps listings whose card location matches one of the configured place names.
///
/// Facebook treats the account's saved Marketplace location as a *preference*, not a
/// boundary — when a search has few local results it pads the feed with out-of-region
/// listings (Tauranga, Hamilton, Napier for an Auckland account). The card's own
/// location string is the only reliable signal we get, so we filter on it directly.
/// </summary>
public static class LocationFilter
{
    public static bool IsAllowed(string location, IReadOnlyList<string> allowedLocations)
    {
        var terms = allowedLocations.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

        // No configuration means the filter is off. Never interpret "unconfigured" as
        // "reject everything" — that would silently stop all alerts.
        if (terms.Count == 0)
        {
            return true;
        }

        return terms.Any(term => location.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
