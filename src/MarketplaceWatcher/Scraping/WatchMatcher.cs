using MarketplaceWatcher.Data;

namespace MarketplaceWatcher.Scraping;

public static class WatchMatcher
{
    public static bool Matches(ScrapedCard card, WatchEntity watch)
    {
        if (watch.FreeOnly)
        {
            if (card.Price != 0m)
            {
                return false;
            }
        }
        else
        {
            if (watch.MinPrice is { } minPrice && card.Price < minPrice)
            {
                return false;
            }

            if (watch.MaxPrice is { } maxPrice && card.Price > maxPrice)
            {
                return false;
            }
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

        return true;
    }
}
