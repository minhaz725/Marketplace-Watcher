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

    /// <summary>
    /// When you marked this listing gone (sold, withdrawn, not worth chasing). Null = still
    /// open. A timestamp rather than a bool because knowing *when* costs nothing extra and
    /// answers "did I check this recently?".
    /// </summary>
    public DateTime? SoldAt { get; set; }

    /// <summary>
    /// When you marked this listing as asked / reached out to the seller. Null = not yet.
    /// Same timestamp-as-bool shape as <see cref="SoldAt"/>; existing rows default to null.
    /// </summary>
    public DateTime? ContactedAt { get; set; }

    /// <summary>
    /// When you removed this listing from the dashboard. Null = visible. A soft delete: the row
    /// must stay because dedup is keyed on Signature in this table, so a hard delete would make
    /// the next poll treat a still-live listing as new and alert on it again.
    /// </summary>
    public DateTime? HiddenAt { get; set; }

    /// <summary>
    /// Which marketplace this came from ("Facebook" or "TradeMe"). Drives the dashboard tabs.
    /// Rows written before Trade Me support default to Facebook via the migration.
    /// </summary>
    public string Source { get; set; } = ListingSources.Facebook;
}
