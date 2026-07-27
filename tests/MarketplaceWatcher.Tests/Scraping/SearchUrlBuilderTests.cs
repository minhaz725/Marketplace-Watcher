using MarketplaceWatcher.Data;
using MarketplaceWatcher.Scraping;
using Xunit;

namespace MarketplaceWatcher.Tests.Scraping;

public class SearchUrlBuilderTests
{
    [Fact]
    public void Builds_a_keyword_search_url()
    {
        var url = SearchUrlBuilder.Build(new WatchEntity { Name = "sofa" }, daysSinceListed: null);

        Assert.Equal("https://www.facebook.com/marketplace/search?query=sofa", url);
    }

    [Fact]
    public void Builds_the_free_category_url_when_the_watch_is_free_only()
    {
        var url = SearchUrlBuilder.Build(new WatchEntity { FreeOnly = true }, daysSinceListed: null);

        Assert.Equal("https://www.facebook.com/marketplace/category/free", url);
    }

    [Fact]
    public void Appends_daysSinceListed_to_a_keyword_search()
    {
        var url = SearchUrlBuilder.Build(new WatchEntity { Name = "sofa" }, daysSinceListed: 1);

        Assert.Equal("https://www.facebook.com/marketplace/search?query=sofa&daysSinceListed=1", url);
    }

    [Fact]
    public void Appends_daysSinceListed_to_the_free_category_as_the_first_parameter()
    {
        // The free category URL has no existing query string, so the separator must be "?".
        var url = SearchUrlBuilder.Build(new WatchEntity { FreeOnly = true }, daysSinceListed: 7);

        Assert.Equal("https://www.facebook.com/marketplace/category/free?daysSinceListed=7", url);
    }

    [Fact]
    public void Escapes_a_multi_word_keyword()
    {
        var url = SearchUrlBuilder.Build(new WatchEntity { Name = "iPhone 16 Pro Max" }, daysSinceListed: 1);

        Assert.Equal(
            "https://www.facebook.com/marketplace/search?query=iPhone%2016%20Pro%20Max&daysSinceListed=1",
            url);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Omits_a_nonsensical_day_window(int days)
    {
        // Guard against a mis-set config silently producing "daysSinceListed=0".
        var url = SearchUrlBuilder.Build(new WatchEntity { Name = "sofa" }, days);

        Assert.Equal("https://www.facebook.com/marketplace/search?query=sofa", url);
    }
}
