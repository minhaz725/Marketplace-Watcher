using System.Globalization;
using System.Text.RegularExpressions;

namespace MarketplaceWatcher.Scraping;

/// <summary>
/// Reads a Trade Me search card from the anchor's href and innerText. The card is one value
/// per line (seller type, optional banners, title, location, odometer, specs, price), so it is
/// parsed by line shape rather than by Trade Me's Angular DOM, which would break on any rebuild.
/// </summary>
public static partial class TradeMeCardParser
{
    private const string Origin = "https://www.trademe.co.nz";

    [GeneratedRegex(@"^(19|20)\d{2}\s+\S")]
    private static partial Regex YearTitle();

    [GeneratedRegex(@"^([\d,]+)\s*km$", RegexOptions.IgnoreCase)]
    private static partial Regex Odometer();

    [GeneratedRegex(@"^\$([\d,]+(\.\d+)?)$")]
    private static partial Regex Price();

    [GeneratedRegex(@"/listing/(\d+)")]
    private static partial Regex ListingIdInPath();

    public static TradeMeCard? Parse(string href, string innerText)
    {
        var lines = innerText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Banners such as "No Photo", "Private seller" or "Closes: 6 hrs" come first, so the
        // title is the first line that starts with a model year.
        var titleIndex = Array.FindIndex(lines, l => YearTitle().IsMatch(l));
        if (titleIndex < 0 || titleIndex + 1 >= lines.Length)
        {
            return null;
        }

        var idMatch = ListingIdInPath().Match(href);
        if (!idMatch.Success)
        {
            return null;
        }

        var after = lines.Skip(titleIndex + 1).ToList();

        // First $ amount wins. An auction shows "Reserve not met $3,500 / Buy now $5,000", and
        // the bid-side number is the one that is comparable to an asking price.
        var priceLine = after.Select(l => Price().Match(l)).FirstOrDefault(m => m.Success);
        if (priceLine is null)
        {
            return null;
        }

        var km = after.Select(l => Odometer().Match(l)).FirstOrDefault(m => m.Success);

        var path = href.Split('?')[0];
        var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : Origin + path;

        return new TradeMeCard(
            idMatch.Groups[1].Value,
            lines[titleIndex],
            decimal.Parse(priceLine.Groups[1].Value.Replace(",", ""), CultureInfo.InvariantCulture),
            lines[titleIndex + 1],
            km is null ? null : int.Parse(km.Groups[1].Value.Replace(",", ""), CultureInfo.InvariantCulture),
            url);
    }
}
