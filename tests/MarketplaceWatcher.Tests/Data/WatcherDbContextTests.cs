using MarketplaceWatcher.Data;
using MarketplaceWatcher.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketplaceWatcher.Tests.Data;

public class WatcherDbContextTests
{
    private static WatcherDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<WatcherDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var context = new WatcherDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task GetNewAsync_ReturnsAllListings_WhenNoneSeenBefore()
    {
        using var context = CreateInMemoryContext();
        var candidates = new[]
        {
            new Listing("Free sofa", 0m, "Mount Eden", "https://facebook.com/marketplace/item/1"),
            new Listing("Sofa", 50m, "Ponsonby", "https://facebook.com/marketplace/item/2"),
        };

        var result = await context.GetNewAsync(candidates);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetNewAsync_ExcludesAlreadySeenListings_OnSecondCall()
    {
        using var context = CreateInMemoryContext();
        var candidates = new[]
        {
            new Listing("Free sofa", 0m, "Mount Eden", "https://facebook.com/marketplace/item/1"),
            new Listing("Sofa", 50m, "Ponsonby", "https://facebook.com/marketplace/item/2"),
        };

        await context.GetNewAsync(candidates);
        var second = await context.GetNewAsync(candidates);

        Assert.Empty(second);
    }

    [Fact]
    public async Task GetNewAsync_CollapsesDuplicateSignaturesWithinASingleBatch()
    {
        // Real failure from a "Full Mirror" watch: Marketplace lists many items with the
        // same title, price and location. Signature deliberately excludes Url (CLAUDE.md
        // section 8), so these collapse to one key — and AddRange used to throw
        // "another instance with the same key value is already being tracked".
        using var context = CreateInMemoryContext();
        var candidates = new[]
        {
            new Listing("Full Mirror", 40m, "Auckland, New Zealand", "https://facebook.com/marketplace/item/1"),
            new Listing("Full Mirror", 40m, "Auckland, New Zealand", "https://facebook.com/marketplace/item/2"),
            new Listing("Full Mirror", 40m, "Auckland, New Zealand", "https://facebook.com/marketplace/item/3"),
        };

        var result = await context.GetNewAsync(candidates);

        Assert.Single(result);
        Assert.Equal(1, await context.Seen.CountAsync());
    }

    [Fact]
    public async Task GetNewAsync_StillExcludesIntraBatchDuplicates_WhenOneWasSeenBefore()
    {
        using var context = CreateInMemoryContext();
        var listing = new Listing("Full Mirror", 40m, "Auckland, New Zealand", "https://facebook.com/marketplace/item/1");
        var sameContentDifferentUrl = listing with { Url = "https://facebook.com/marketplace/item/2" };

        await context.GetNewAsync([listing]);
        var second = await context.GetNewAsync([listing, sameContentDifferentUrl]);

        Assert.Empty(second);
    }

    [Fact]
    public async Task GetNewAsync_ReturnsOnlyTheGenuinelyNewListing_WhenMixedWithSeenOnes()
    {
        using var context = CreateInMemoryContext();
        var seenBefore = new Listing("Free sofa", 0m, "Mount Eden", "https://facebook.com/marketplace/item/1");
        var brandNew = new Listing("iPhone 16 Pro Max", 800m, "Newmarket", "https://facebook.com/marketplace/item/3");

        await context.GetNewAsync([seenBefore]);
        var result = await context.GetNewAsync([seenBefore, brandNew]);

        Assert.Equal([brandNew], result);
    }
}
