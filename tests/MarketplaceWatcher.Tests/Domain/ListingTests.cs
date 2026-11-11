using MarketplaceWatcher.Domain;

namespace MarketplaceWatcher.Tests.Domain;

public class ListingTests
{
    [Fact]
    public void Signature_IsStableForIdenticalContent()
    {
        var a = new Listing("Free sofa", 0m, "Mount Eden", "https://facebook.com/marketplace/item/1");
        var b = new Listing("Free sofa", 0m, "Mount Eden", "https://facebook.com/marketplace/item/1");

        Assert.Equal(a.Signature, b.Signature);
    }

    [Fact]
    public void Signature_IgnoresUrlDifferences()
    {
        var a = new Listing("Free sofa", 0m, "Mount Eden", "https://facebook.com/marketplace/item/1");
        var b = new Listing("Free sofa", 0m, "Mount Eden", "https://facebook.com/marketplace/item/2");

        Assert.Equal(a.Signature, b.Signature);
    }

    [Fact]
    public void Signature_IgnoresCaseAndSurroundingWhitespace()
    {
        var a = new Listing("Free sofa", 0m, "Mount Eden", "https://facebook.com/marketplace/item/1");
        var b = new Listing("  FREE SOFA  ", 0m, " mount eden ", "https://facebook.com/marketplace/item/2");

        Assert.Equal(a.Signature, b.Signature);
    }

    [Fact]
    public void Signature_DiffersWhenTitleDiffers()
    {
        var a = new Listing("Free sofa", 0m, "Mount Eden", "https://facebook.com/marketplace/item/1");
        var b = new Listing("Free couch", 0m, "Mount Eden", "https://facebook.com/marketplace/item/1");

        Assert.NotEqual(a.Signature, b.Signature);
    }

    [Fact]
    public void Signature_DiffersWhenPriceDiffers()
    {
        var a = new Listing("Sofa", 50m, "Mount Eden", "https://facebook.com/marketplace/item/1");
        var b = new Listing("Sofa", 60m, "Mount Eden", "https://facebook.com/marketplace/item/1");

        Assert.NotEqual(a.Signature, b.Signature);
    }
}

public class ListingSourceSignatureTests
{
    [Fact]
    public void A_facebook_signature_is_the_plain_hash_so_stored_rows_still_match()
    {
        // Same inputs through the old formula: lowercased title|price|location, SHA-256, hex.
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("honda fit|3000|auckland"));
        var legacy = Convert.ToHexString(bytes);

        Assert.Equal(legacy, new MarketplaceWatcher.Domain.Listing("Honda Fit", 3000m, "Auckland", "u").Signature);
    }

    [Fact]
    public void The_same_listing_on_trade_me_has_a_different_signature()
    {
        var fb = new MarketplaceWatcher.Domain.Listing("Honda Fit", 3000m, "Auckland", "u");
        var tm = fb with { Source = MarketplaceWatcher.Data.ListingSources.TradeMe };

        Assert.NotEqual(fb.Signature, tm.Signature);
    }
}
