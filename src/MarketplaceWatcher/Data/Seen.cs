namespace MarketplaceWatcher.Data;

/// <summary>
/// A listing we have already alerted on. Started life in Phase 4 as a pure dedup ledger
/// (signature + title were all the poll loop needed); Phase 13 added the display fields so
/// the UI can show history. They are nullable because rows written before that change exist.
/// </summary>
public sealed class Seen
{
    public required string Signature { get; set; }
    public required string Title { get; set; }
    /// <summary>
    /// UTC. Deliberately DateTime rather than DateTimeOffset: SQLite cannot ORDER BY a
    /// DateTimeOffset (it has no date type and EF stores the offset in the text), and
    /// /api/listings needs newest-first. We only ever write UtcNow, so no information is lost.
    /// </summary>
    public required DateTime SeenAt { get; set; }

    public decimal? Price { get; set; }
    public string? Location { get; set; }
    public string? Url { get; set; }

    /// <summary>
    /// The watch that matched, captured at match time. Denormalized on purpose: history should
    /// show what it matched *then*, even if the watch is later renamed or deleted.
    /// </summary>
    public int? WatchId { get; set; }

    public string? WatchName { get; set; }
}
