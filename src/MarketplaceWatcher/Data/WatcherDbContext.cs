using System.Text.Json;
using MarketplaceWatcher.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MarketplaceWatcher.Data;

public sealed class WatcherDbContext(DbContextOptions<WatcherDbContext> options) : DbContext(options)
{
    public DbSet<Seen> Seen => Set<Seen>();
    public DbSet<WatchEntity> Watches => Set<WatchEntity>();
    public DbSet<CheckedListing> CheckedListings => Set<CheckedListing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Seen>().HasKey(s => s.Signature);
        modelBuilder.Entity<CheckedListing>().HasKey(c => c.ListingId);

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
        // Collapse duplicates *within* the batch before touching the DB. Signature excludes
        // Url by design (CLAUDE.md section 8), so several distinct Marketplace listings with
        // the same title/price/location are one item as far as dedup is concerned. Without
        // this, AddRange gets two entities sharing a primary key and EF throws.
        var candidateList = candidates.DistinctBy(c => c.Signature).ToList();
        var signatures = candidateList.Select(c => c.Signature).ToList();

        var alreadySeen = await Seen
            .Where(s => signatures.Contains(s.Signature))
            .Select(s => s.Signature)
            .ToHashSetAsync(cancellationToken);

        // Read-only on purpose. Recording happens in MarkSeenAsync *after* a successful
        // notification, so a failed send leaves the listing unseen and it is retried next
        // poll. Committing here would mean a Telegram outage silently loses alerts.
        return candidateList.Where(c => !alreadySeen.Contains(c.Signature)).ToList();
    }

    /// <summary>
    /// Records listings as seen. Call only for listings whose alert was actually delivered.
    /// </summary>
    public async Task MarkSeenAsync(
        IEnumerable<Listing> listings,
        int? watchId = null,
        string? watchName = null,
        CancellationToken cancellationToken = default)
    {
        var distinct = listings.DistinctBy(l => l.Signature).ToList();
        if (distinct.Count == 0)
        {
            return;
        }

        // Re-check the DB: a signature could already be present from an earlier watch in the
        // same poll, and adding a duplicate key would throw.
        var signatures = distinct.Select(l => l.Signature).ToList();
        var existing = await Seen
            .Where(s => signatures.Contains(s.Signature))
            .Select(s => s.Signature)
            .ToHashSetAsync(cancellationToken);

        Seen.AddRange(distinct
            .Where(l => !existing.Contains(l.Signature))
            .Select(l => new Data.Seen
            {
                Signature = l.Signature,
                Title = l.Title,
                SeenAt = DateTime.UtcNow,
                Price = l.Price,
                Location = l.Location,
                Url = l.Url,
                WatchId = watchId,
                WatchName = watchName,
            }));

        await SaveChangesAsync(cancellationToken);
    }
}
