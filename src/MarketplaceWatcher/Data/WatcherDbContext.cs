using MarketplaceWatcher.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketplaceWatcher.Data;

public sealed class WatcherDbContext(DbContextOptions<WatcherDbContext> options) : DbContext(options)
{
    public DbSet<Seen> Seen => Set<Seen>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Seen>().HasKey(s => s.Signature);
    }

    public async Task<List<Listing>> GetNewAsync(IEnumerable<Listing> candidates, CancellationToken cancellationToken = default)
    {
        var candidateList = candidates.ToList();
        var signatures = candidateList.Select(c => c.Signature).ToList();

        var alreadySeen = await Seen
            .Where(s => signatures.Contains(s.Signature))
            .Select(s => s.Signature)
            .ToHashSetAsync(cancellationToken);

        var newListings = candidateList.Where(c => !alreadySeen.Contains(c.Signature)).ToList();

        Seen.AddRange(newListings.Select(l => new Data.Seen
        {
            Signature = l.Signature,
            Title = l.Title,
            SeenAt = DateTimeOffset.UtcNow,
        }));
        await SaveChangesAsync(cancellationToken);

        return newListings;
    }
}
