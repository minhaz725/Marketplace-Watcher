using MarketplaceWatcher.Scraping;
using Xunit;

namespace MarketplaceWatcher.Tests.Scraping;

public class CardParserTests
{
    // Fixtures below are real aria-label values captured from Facebook Marketplace
    // (facebook.com/marketplace/category/free and a "sofa" search), not invented.

    [Fact]
    public void Parses_a_free_listing()
    {
        var result = CardParser.Parse(
            "PS4+console+with+controller+and+PS3+console+no+controller+make+me+a+offer, NZ$0, Auckland, New Zealand, listing 1673919483683613");

        Assert.NotNull(result);
        Assert.Equal("PS4+console+with+controller+and+PS3+console+no+controller+make+me+a+offer", result!.Title);
        Assert.Equal(0m, result.Price);
        Assert.Equal("Auckland, New Zealand", result.Location);
        Assert.Equal("1673919483683613", result.ListingId);
    }

    [Fact]
    public void Parses_a_priced_listing()
    {
        var result = CardParser.Parse(
            "Sofa, NZ$30, Auckland, New Zealand, listing 1349379546738767");

        Assert.NotNull(result);
        Assert.Equal("Sofa", result!.Title);
        Assert.Equal(30m, result.Price);
        Assert.Equal("Auckland, New Zealand", result.Location);
        Assert.Equal("1349379546738767", result.ListingId);
    }

    [Fact]
    public void Parses_a_title_that_itself_looks_malformed()
    {
        // Real captured example — seller likely typed a broken title ("-Seater Recliner Sofa"
        // instead of "3-Seater..."). Confirms the parser doesn't choke on odd but real titles.
        var result = CardParser.Parse(
            "-Seater Recliner Sofa, NZ$80, Auckland, New Zealand, listing 2152706225291895");

        Assert.NotNull(result);
        Assert.Equal("-Seater Recliner Sofa", result!.Title);
        Assert.Equal(80m, result.Price);
    }

    [Fact]
    public void Parses_a_larger_price_with_thousands_separator()
    {
        var result = CardParser.Parse(
            "iPhone 16 Pro Max, NZ$1,200, Auckland, New Zealand, listing 9999999999999");

        Assert.NotNull(result);
        Assert.Equal(1200m, result!.Price);
    }

    [Fact]
    public void Returns_null_for_unrecognised_format()
    {
        var result = CardParser.Parse("not a marketplace aria-label at all");

        Assert.Null(result);
    }
}
