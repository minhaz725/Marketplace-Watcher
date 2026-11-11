namespace MarketplaceWatcher.Scraping;

public static class TradeMeExpiryRule
{
    /// <summary>
    /// True if <paramref name="expiry"/> is at least <paramref name="minMonths"/> after
    /// <paramref name="today"/>. An unknown date passes: the alert says so and you check it
    /// yourself rather than the car being silently dropped.
    /// </summary>
    public static bool Passes(DateTime? expiry, DateTime today, int minMonths) =>
        expiry is null || expiry.Value.Date >= today.Date.AddMonths(minMonths);
}
