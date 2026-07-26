using MarketplaceWatcher.Data;
using Microsoft.EntityFrameworkCore;

namespace MarketplaceWatcher.Tests.Data;

public class WatchEntityTests
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
    public async Task ExcludeKeywords_RoundTrip_PreservesArrayContents()
    {
        using var context = CreateInMemoryContext();
        context.Watches.Add(new WatchEntity
        {
            Name = "sofa",
            ExcludeKeywords = ["wanted", "parts", "cover only"],
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        // New context instance sharing the same open connection forces a real read from
        // the DB rather than the change tracker's in-memory copy, proving the JSON
        // conversion round-trips correctly.
        using var reloaded = new WatcherDbContext(
            new DbContextOptionsBuilder<WatcherDbContext>()
                .UseSqlite(context.Database.GetDbConnection())
                .Options);

        var watch = await reloaded.Watches.SingleAsync();

        Assert.Equal(["wanted", "parts", "cover only"], watch.ExcludeKeywords);
    }

    [Fact]
    public async Task Query_ReturnsOnlyActiveWatches()
    {
        using var context = CreateInMemoryContext();
        context.Watches.AddRange(
            new WatchEntity { Name = "active-one", IsActive = true, CreatedAt = DateTimeOffset.UtcNow },
            new WatchEntity { Name = "deactivated", IsActive = false, CreatedAt = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync();

        var active = await context.Watches.Where(w => w.IsActive).ToListAsync();

        Assert.Single(active);
        Assert.Equal("active-one", active[0].Name);
    }
}
