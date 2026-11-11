using MarketplaceWatcher.Data;
using MarketplaceWatcher.Scraping;

namespace MarketplaceWatcher.Tests.Scraping;

public class TradeMeMatcherTests
{
    private static TradeMeCard Card(string title = "2005 Honda Fit", decimal price = 3000m, int? km = 150_000) =>
        new("1", title, price, "Auckland City, Auckland", km, "https://www.trademe.co.nz/x/1");

    private static WatchEntity Watch() => new()
    {
        Name = "Honda Fit",
        MinPrice = 1500m,
        MaxPrice = 3500m,
        MustIncludeKeywords = ["Fit"],
        ExcludeKeywords = ["wanted", "parts"],
        MaxOdometerKm = 200_000,
    };

    [Fact]
    public void A_card_inside_every_limit_matches() => Assert.True(TradeMeMatcher.Matches(Card(), Watch()));

    [Theory]
    [InlineData(1499, false)]
    [InlineData(1500, true)]
    [InlineData(3500, true)]
    [InlineData(3501, false)]
    public void Price_band_is_inclusive(decimal price, bool expected) =>
        Assert.Equal(expected, TradeMeMatcher.Matches(Card(price: price), Watch()));

    [Theory]
    [InlineData(200_000, true)]
    [InlineData(200_001, false)]
    public void Odometer_limit_is_inclusive(int km, bool expected) =>
        Assert.Equal(expected, TradeMeMatcher.Matches(Card(km: km), Watch()));

    [Fact]
    public void Unknown_odometer_passes_so_you_can_check_it_yourself() =>
        Assert.True(TradeMeMatcher.Matches(Card(km: null), Watch()));

    [Fact]
    public void No_odometer_limit_on_the_watch_means_any_km()
    {
        var watch = Watch();
        watch.MaxOdometerKm = null;

        Assert.True(TradeMeMatcher.Matches(Card(km: 999_999), watch));
    }

    [Fact]
    public void Must_include_and_exclude_apply_to_the_title()
    {
        Assert.False(TradeMeMatcher.Matches(Card(title: "2005 Honda Jazz"), Watch()));
        Assert.False(TradeMeMatcher.Matches(Card(title: "2005 Honda Fit for PARTS"), Watch()));
    }
}
