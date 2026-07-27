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
    /// <summary>
    /// Price rules only. Used as the cheap gate before deciding whether a titleless listing
    /// is worth opening its own page to recover a title — no point paying for a page load on
    /// a car that is out of budget anyway.
    /// </summary>
    public static bool MatchesPrice(ScrapedCard card, WatchEntity watch)
    {
        if (watch.FreeOnly)
        {
            return card.Price == 0m;
        }

        if (watch.MinPrice is { } minPrice && card.Price < minPrice)
        {
            return false;
        }

        return watch.MaxPrice is not { } maxPrice || card.Price <= maxPrice;
    }

    public static bool Matches(ScrapedCard card, WatchEntity watch)
    {
        if (!MatchesPrice(card, watch))
        {
            return false;
        }

        // Roughly half of vehicle listings render no title on the search grid, so neither
        // keyword filter can be evaluated for them. We drop them.
        //
        // This reverses the first attempt, which let them through so nothing was lost. In
        // practice the unfilterable listings were mostly rubbish — with excludeKeywords
        // inert, every wreck, parts lot and mispriced decoy came through. Precision beat
        // coverage. See ADR-005. The funnel log still reports how many were dropped, so this
        // is a visible tradeoff rather than the silent discard it used to be.
        if (!card.HasTitle)
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

        return true;
    }
}
