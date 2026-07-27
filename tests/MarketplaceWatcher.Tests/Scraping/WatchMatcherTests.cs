using MarketplaceWatcher.Data;
using MarketplaceWatcher.Scraping;
using Xunit;

namespace MarketplaceWatcher.Tests.Scraping;

public class WatchMatcherTests
{
    [Fact]
    public void A_titleless_card_still_matches_when_the_price_fits()
    {
        // Facebook renders no title for many vehicle listings. Keyword filters cannot be
        // evaluated, so we fail open rather than discard the listing entirely.
        var card = new ScrapedCard("", 6999m, "Auckland, New Zealand", "2054673432081515");
        var watch = new WatchEntity
        {
            Name = "Toyota Prius",
            MinPrice = 1000m,
            MaxPrice = 7000m,
            MustIncludeKeywords = ["prius"],
            ExcludeKeywords = ["wrecking", "parts"],
        };

        Assert.True(WatchMatcher.Matches(card, watch));
    }

    [Fact]
    public void A_titleless_card_is_still_rejected_on_price()
    {
        // Failing open applies to keywords only — price bounds are unaffected.
        var card = new ScrapedCard("", 13500m, "Auckland, New Zealand", "1351174057145221");
        var watch = new WatchEntity { Name = "Toyota Prius", MinPrice = 1000m, MaxPrice = 7000m };

        Assert.False(WatchMatcher.Matches(card, watch));
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
