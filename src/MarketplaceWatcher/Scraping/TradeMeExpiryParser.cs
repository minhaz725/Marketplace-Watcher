using System.Globalization;
using System.Text.RegularExpressions;

namespace MarketplaceWatcher.Scraping;

/// <summary>
/// Reads "Registration expires: Mar 2027" and "WoF expires: Oct 2027" from a listing page's
/// text. Trade Me shows month and year only, so each is returned as the LAST day of that
/// month: the car is given the benefit of the doubt about which day it really expires.
/// </summary>
public static partial class TradeMeExpiryParser
{
    [GeneratedRegex(@"Registration expires:\s*([A-Za-z]{3,9})\s+(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex Rego();

    [GeneratedRegex(@"WoF expires:\s*([A-Za-z]{3,9})\s+(\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex Wof();

    public static (DateTime? Rego, DateTime? Wof) Parse(string pageText) =>
        (ReadMonth(Rego(), pageText), ReadMonth(Wof(), pageText));

    private static DateTime? ReadMonth(Regex pattern, string text)
    {
        var match = pattern.Match(text);
        if (!match.Success)
        {
            return null;
        }

        // "Mar" and "March" both parse; anything unrecognised is treated as not shown.
        if (!DateTime.TryParseExact(
                $"{match.Groups[1].Value} {match.Groups[2].Value}",
                ["MMM yyyy", "MMMM yyyy"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var firstOfMonth))
        {
            return null;
        }

        return firstOfMonth.AddMonths(1).AddDays(-1);
    }
}
