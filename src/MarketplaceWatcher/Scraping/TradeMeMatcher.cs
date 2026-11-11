using MarketplaceWatcher.Data;

namespace MarketplaceWatcher.Scraping;

/// <summary>
/// The card-level filters for a Trade Me watch: price, title keywords and odometer. WOF and
/// rego need the listing page, so they are applied later and only to cars that get this far.
/// </summary>
public static class TradeMeMatcher
{
    public static bool Matches(TradeMeCard card, WatchEntity watch)
    {
        if (watch.MinPrice is { } min && card.Price < min)
        {
            return false;
        }

        if (watch.MaxPrice is { } max && card.Price > max)
        {
            return false;
        }

        if (watch.MustIncludeKeywords.Length > 0 &&
            !watch.MustIncludeKeywords.All(k => card.Title.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (watch.ExcludeKeywords.Any(k => card.Title.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // An odometer the card does not show is let through — you check it yourself.
        return watch.MaxOdometerKm is not { } maxKm || card.OdometerKm is not { } km || km <= maxKm;
    }
}
