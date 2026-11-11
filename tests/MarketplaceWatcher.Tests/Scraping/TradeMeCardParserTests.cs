using MarketplaceWatcher.Scraping;

namespace MarketplaceWatcher.Tests.Scraping;

public class TradeMeCardParserTests
{
    private const string Href = "/a/motors/cars/honda/fit/listing/6174459004?rsqid=d70b87ac36a04c6484390736c84aaf65-001";

    [Fact]
    public void Parses_an_asking_price_card()
    {
        // Real card text (innerText of the <a>), one value per line.
        var text = "Private seller\n2005 Honda Fit\nManukau, Auckland\n240,916 km\nPetrol\nAutomatic\n1,330 cc\nAsking price\n$3,500\nIncludes on road costs";

        var card = TradeMeCardParser.Parse(Href, text);

        Assert.NotNull(card);
        Assert.Equal("2005 Honda Fit", card.Title);
        Assert.Equal("Manukau, Auckland", card.Location);
        Assert.Equal(240_916, card.OdometerKm);
        Assert.Equal(3500m, card.Price);
        Assert.Equal("6174459004", card.ListingId);
    }

    [Fact]
    public void Strips_the_tracking_query_and_makes_the_url_absolute()
    {
        var text = "Private seller\n2005 Honda Fit\nManukau, Auckland\n240,916 km\nAsking price\n$3,500";

        var card = TradeMeCardParser.Parse(Href, text);

        Assert.Equal("https://www.trademe.co.nz/a/motors/cars/honda/fit/listing/6174459004", card!.Url);
    }

    [Fact]
    public void Skips_the_extra_banner_lines_before_the_title()
    {
        var text = "No Photo\nPrivate seller\nListed within the last 7 days\n2005 Honda Fit\nAuckland City, Auckland\n201,000 km\nPetrol\nAutomatic\n1,330 cc\nAsking price\n$2,900";

        var card = TradeMeCardParser.Parse(Href, text);

        Assert.Equal("2005 Honda Fit", card!.Title);
        Assert.Equal("Auckland City, Auckland", card.Location);
    }

    [Fact]
    public void An_auction_takes_the_first_price_shown_not_the_buy_now()
    {
        var text = "Private seller\n2006 Honda Fit\nCambridge, Waikato\n146,900 km\nPetrol\nAutomatic\n1,330 cc\nReserve not met\n$3,500\nBuy now\n$5,000";

        var card = TradeMeCardParser.Parse(Href, text);

        Assert.Equal(3500m, card!.Price);
    }

    [Fact]
    public void A_card_with_no_odometer_still_parses_with_a_null_odometer()
    {
        var text = "Private seller\n2005 Honda Fit\nAuckland City, Auckland\nAsking price\n$3,000";

        var card = TradeMeCardParser.Parse(Href, text);

        Assert.NotNull(card);
        Assert.Null(card.OdometerKm);
    }

    [Theory]
    [InlineData("Private seller\n2005 Honda Fit\nAuckland City, Auckland\n100,000 km")]   // no price
    [InlineData("Private seller\nHonda Fit for sale\nAuckland\n100,000 km\n$3,000")]      // no year-led title
    [InlineData("")]
    public void Returns_null_when_the_card_cannot_be_read(string text)
    {
        Assert.Null(TradeMeCardParser.Parse(Href, text));
    }
}
