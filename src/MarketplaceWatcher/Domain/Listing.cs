using System.Security.Cryptography;
using System.Text;

namespace MarketplaceWatcher.Domain;

public sealed record Listing(string Title, decimal Price, string Location, string Url)
{
    public string Signature => ComputeSignature(Title, Price, Location);

    private static string ComputeSignature(string title, decimal price, string location)
    {
        var normalized = $"{title.Trim().ToLowerInvariant()}|{price}|{location.Trim().ToLowerInvariant()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }
}
