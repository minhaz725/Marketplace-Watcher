using MarketplaceWatcher.Data;

namespace MarketplaceWatcher.Scraping;

public static class TradeMeSearchUrlBuilder
{
    /// <summary>
    /// "Honda Fit" -> .../cars/honda/fit/search. The first word is the make and the rest is
    /// the model, which holds for every car watch. Returns null for a watch that is not a
    /// make-and-model search (e.g. "Fan"), so it is skipped rather than searched wrongly.
    /// </summary>
    public static string? Build(WatchEntity watch)
    {
        var words = (watch.Name ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length < 2)
        {
            return null;
        }

        var make = words[0].ToLowerInvariant();
        var model = string.Join('-', words.Skip(1)).ToLowerInvariant();

        var query = new List<string>();
        if (watch.MinPrice is { } min) query.Add($"price_min={min:0.##}");
        if (watch.MaxPrice is { } max) query.Add($"price_max={max:0.##}");

        // Newest first, so the first page holds the freshest cars.
        query.Add("sort_order=MotorsLatestListings");

        return $"https://www.trademe.co.nz/a/motors/cars/{make}/{model}/search?{string.Join('&', query)}";
    }
}
