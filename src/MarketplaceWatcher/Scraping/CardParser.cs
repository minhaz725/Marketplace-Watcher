using System.Text.RegularExpressions;

namespace MarketplaceWatcher.Scraping;

public sealed record ScrapedCard(string Title, decimal Price, string Location, string ListingId)
{
    /// <summary>
    /// False when Facebook rendered no title on the card. Keyword filters cannot be
    /// evaluated for those listings — see WatchMatcher for how that is handled.
    /// </summary>
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
}

public static partial class CardParser
{
    // Facebook Marketplace's aria-label on each listing card reads:
    // "{title}, NZ${price}, {location}, listing {id}"
    // Location itself may contain a comma (e.g. "Auckland, New Zealand"), so title is
    // matched non-greedily up to the first ", NZ$" and location is everything left
    // between the price and the fixed ", listing {id}" suffix.
    //
    // Title is `.*?` not `.+?`: about half of vehicle listings render no title on the search
    // grid at all, giving a label that starts ", NZ$...". Requiring a title made those parse
    // as null and be dropped without trace.
    [GeneratedRegex(@"^(?<title>.*?), NZ\$(?<price>[\d,]+), (?<location>.+), listing (?<id>\d+)$")]
    private static partial Regex AriaLabelPattern();

    public static ScrapedCard? Parse(string ariaLabel)
    {
        var match = AriaLabelPattern().Match(ariaLabel);
        if (!match.Success)
        {
            return null;
        }

        var title = match.Groups["title"].Value;
        var price = decimal.Parse(match.Groups["price"].Value.Replace(",", ""));
        var location = match.Groups["location"].Value;
        var listingId = match.Groups["id"].Value;

        return new ScrapedCard(title, price, location, listingId);
    }
}
