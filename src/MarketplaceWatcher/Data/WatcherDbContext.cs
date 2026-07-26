using System.Text.Json;
using MarketplaceWatcher.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MarketplaceWatcher.Data;

public sealed class WatcherDbContext(DbContextOptions<WatcherDbContext> options) : DbContext(options)
{
    public DbSet<Seen> Seen => Set<Seen>();
    public DbSet<WatchEntity> Watches => Set<WatchEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Seen>().HasKey(s => s.Signature);

        var stringArrayComparer = new ValueComparer<string[]>(
            (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
            a => a.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            a => a.ToArray());

        modelBuilder.Entity<WatchEntity>().Property(w => w.MustIncludeKeywords)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>())
            .Metadata.SetValueComparer(stringArrayComparer);

        modelBuilder.Entity<WatchEntity>().Property(w => w.ExcludeKeywords)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>())
            .Metadata.SetValueComparer(stringArrayComparer);
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
