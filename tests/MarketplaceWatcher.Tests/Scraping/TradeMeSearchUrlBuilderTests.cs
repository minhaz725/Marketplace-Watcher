using MarketplaceWatcher.Data;
using MarketplaceWatcher.Scraping;

namespace MarketplaceWatcher.Tests.Scraping;

public class TradeMeSearchUrlBuilderTests
{
    [Fact]
    public void First_word_is_the_make_and_the_rest_the_model()
    {
        var url = TradeMeSearchUrlBuilder.Build(new WatchEntity { Name = "Honda Fit", MinPrice = 1500m, MaxPrice = 3500m });

        Assert.Equal(
            "https://www.trademe.co.nz/a/motors/cars/honda/fit/search?price_min=1500&price_max=3500&sort_order=MotorsLatestListings",
            url);
    }

    [Fact]
    public void Omits_price_bounds_that_are_not_set()
    {
        var url = TradeMeSearchUrlBuilder.Build(new WatchEntity { Name = "Mazda Demio", MaxPrice = 3500m });

        Assert.Equal(
            "https://www.trademe.co.nz/a/motors/cars/mazda/demio/search?price_max=3500&sort_order=MotorsLatestListings",
            url);
    }

    [Fact]
    public void A_watch_without_make_and_model_has_no_trademe_search()
    {
        Assert.Null(TradeMeSearchUrlBuilder.Build(new WatchEntity { Name = "Fan" }));
        Assert.Null(TradeMeSearchUrlBuilder.Build(new WatchEntity { Name = null }));
    }
}
