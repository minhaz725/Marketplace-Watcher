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
