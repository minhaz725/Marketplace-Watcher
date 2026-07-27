using MarketplaceWatcher.Data;

namespace MarketplaceWatcher.Scraping;

public static class WatchMatcher
{
    // NOTE: watch.RadiusKm is deliberately NOT applied here, and that is not an omission.
    // Phase 10 reconnaissance established two things against live data:
    //   1. Marketplace ignores latitude/longitude/radius URL params — verified against a
    //      run-to-run noise floor, so radius is enforced by the *account's* saved location.
    //   2. 96% of harvested cards report Location as "Auckland, New Zealand" (154 of 161),
    //      so there is no per-listing position to measure a distance from.
    // Radius is therefore environmental. See PROGRESS.md Phase 10.
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

        // Roughly half of vehicle listings have no title on the card, so neither keyword
        // filter can be evaluated. We let those through (fail open) rather than drop them:
        // the search query already targeted the item, and dropping meant losing most real
        // cars. Evidence for that choice — in a captured "Toyota Prius" harvest the cards
        // WITH titles were overwhelmingly accessories (tail lights, seat covers), while the
        // titleless ones were priced like actual vehicles.
        //
        // The cost is real and worth stating: an excluded word in a titleless listing cannot
        // be filtered, so some wrecks and parts will get through. Price bounds still apply.
        if (!card.HasTitle)
        {
            return true;
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
