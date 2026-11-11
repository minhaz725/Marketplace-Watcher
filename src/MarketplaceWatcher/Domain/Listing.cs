using System.Security.Cryptography;
using System.Text;
using MarketplaceWatcher.Data;

namespace MarketplaceWatcher.Domain;

/// <param name="Source">Which marketplace it came from. Facebook is the default so existing call sites are unchanged.</param>
/// <param name="Note">Extra line for the alert (e.g. Trade Me odometer / WOF / rego). Not part of the signature.</param>
public sealed record Listing(
    string Title,
    decimal Price,
    string Location,
    string Url,
    string Source = ListingSources.Facebook,
    string? Note = null)
{
    public string Signature => ComputeSignature(Source, Title, Price, Location);

    private static string ComputeSignature(string source, string title, decimal price, string location)
    {
        var normalized = $"{title.Trim().ToLowerInvariant()}|{price}|{location.Trim().ToLowerInvariant()}";

        // Facebook signatures must stay exactly as they were or every stored row would stop
        // matching and re-alert. Other sources are namespaced so the same title/price/location
        // on two marketplaces is not treated as one listing.
        if (source != ListingSources.Facebook)
        {
            normalized = $"{source}|{normalized}";
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }
}
