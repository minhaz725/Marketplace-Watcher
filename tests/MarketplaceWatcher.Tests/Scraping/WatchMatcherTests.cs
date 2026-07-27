using MarketplaceWatcher.Data;
using MarketplaceWatcher.Scraping;
using Xunit;

namespace MarketplaceWatcher.Tests.Scraping;

public class WatchMatcherTests
{
    [Theory]
    [InlineData(6999, true)]
    [InlineData(1000, true)]   // inclusive lower bound
    [InlineData(7000, true)]   // inclusive upper bound
    [InlineData(999, false)]
    [InlineData(7001, false)]
    public void MatchesPrice_gates_on_the_band_alone(decimal price, bool expected)
    {
        // The cheap gate before spending a page load recovering a missing title.
        var card = new ScrapedCard("", price, "Auckland, New Zealand", "1");
        var watch = new WatchEntity { MinPrice = 1000m, MaxPrice = 7000m, ExcludeKeywords = ["wrecking"] };

        Assert.Equal(expected, WatchMatcher.MatchesPrice(card, watch));
    }

    [Fact]
    public void MatchesPrice_honours_freeOnly()
    {
        var watch = new WatchEntity { FreeOnly = true };

        Assert.True(WatchMatcher.MatchesPrice(new ScrapedCard("", 0m, "Auckland", "1"), watch));
        Assert.False(WatchMatcher.MatchesPrice(new ScrapedCard("", 10m, "Auckland", "2"), watch));
    }

    [Fact]
    public void A_titleless_card_is_rejected_even_when_the_price_fits()
    {
        // Facebook renders no title for many vehicle listings, so excludeKeywords cannot be
        // applied. Letting them through was tried and produced mostly rubbish (wrecks, parts
        // lots, decoys) — precision beat coverage. See ADR-005.
        var card = new ScrapedCard("", 6999m, "Auckland, New Zealand", "2054673432081515");
        var watch = new WatchEntity
        {
            Name = "Toyota Prius",
            MinPrice = 1000m,
            MaxPrice = 7000m,
            ExcludeKeywords = ["wrecking", "parts"],
        };

        Assert.False(WatchMatcher.Matches(card, watch));
    }

    [Fact]
    public void A_titleless_card_is_rejected_even_with_no_filters_configured()
    {
        // Not a side effect of some other filter — the missing title alone disqualifies it.
        var card = new ScrapedCard("", 6999m, "Auckland, New Zealand", "2054673432081515");

        Assert.False(WatchMatcher.Matches(card, new WatchEntity { Name = "Toyota Prius" }));
    }

    private static ScrapedCard Card(string title, decimal price) =>
        new(title, price, "Auckland, New Zealand", "123");

    private static WatchEntity Watch(
        bool freeOnly = false,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string[]? mustIncludeKeywords = null,
        string[]? excludeKeywords = null) => new()
    {
        FreeOnly = freeOnly,
        MinPrice = minPrice,
        MaxPrice = maxPrice,
        MustIncludeKeywords = mustIncludeKeywords ?? [],
        ExcludeKeywords = excludeKeywords ?? [],
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void FreeOnlyWatch_MatchesZeroPricedCard()
    {
        Assert.True(WatchMatcher.Matches(Card("Free sofa", 0m), Watch(freeOnly: true)));
    }

    [Fact]
    public void FreeOnlyWatch_RejectsPricedCard()
    {
        Assert.False(WatchMatcher.Matches(Card("Sofa", 30m), Watch(freeOnly: true)));
    }

    [Fact]
    public void PriceRange_RejectsCardBelowMinPrice()
    {
        Assert.False(WatchMatcher.Matches(Card("Sofa", 10m), Watch(minPrice: 50m, maxPrice: 200m)));
    }

    [Fact]
    public void PriceRange_RejectsCardAboveMaxPrice()
    {
        Assert.False(WatchMatcher.Matches(Card("Sofa", 300m), Watch(minPrice: 50m, maxPrice: 200m)));
    }

    [Fact]
    public void PriceRange_MatchesCardWithinRange()
    {
        Assert.True(WatchMatcher.Matches(Card("Sofa", 100m), Watch(minPrice: 50m, maxPrice: 200m)));
    }

    [Fact]
    public void ExcludeKeywords_RejectsCardWhoseTitleContainsOne_CaseInsensitive()
    {
        var watch = Watch(excludeKeywords: ["wanted", "parts"]);

        Assert.False(WatchMatcher.Matches(Card("WANTED: sofa", 50m), watch));
    }

    [Fact]
    public void MustIncludeKeywords_RejectsCardMissingOne()
    {
        var watch = Watch(mustIncludeKeywords: ["pro max"]);

        Assert.False(WatchMatcher.Matches(Card("iPhone 16", 800m), watch));
    }

    [Fact]
    public void MustIncludeKeywords_MatchesCardContainingAll_CaseInsensitive()
    {
        var watch = Watch(mustIncludeKeywords: ["pro max"]);

        Assert.True(WatchMatcher.Matches(Card("iPhone 16 PRO MAX", 800m), watch));
    }

    [Fact]
    public void NoFilters_MatchesAnyCard()
    {
        Assert.True(WatchMatcher.Matches(Card("Anything", 999m), Watch()));
    }
}
