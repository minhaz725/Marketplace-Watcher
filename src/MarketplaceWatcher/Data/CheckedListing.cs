namespace MarketplaceWatcher.Data;

/// <summary>
/// Records that we already opened a listing's own page to recover a title the search grid
/// did not render. Keyed on ListingId so each listing is fetched **at most once, ever**.
///
/// This is the cache that makes title enrichment viable. Without it, a listing that gets
/// fetched and then rejected is never written to Seen, so it would be re-fetched on every
/// poll — roughly 960 page loads a day for one rejected car at a 90-second interval.
///
/// Deliberately separate from Seen: Seen is keyed on content signature and holds only
/// listings we alerted on. This has to remember rejections too, under a different key.
/// </summary>
public sealed class CheckedListing
{
    public required string ListingId { get; set; }

    /// <summary>The title read from the listing page, or null if it had none either.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// Trade Me only (key is "tm:{id}"): registration / WOF expiry read from the listing page,
    /// as the last day of the month shown. Null = the page did not show one.
    /// </summary>
    public DateTime? RegoExpires { get; set; }
    public DateTime? WofExpires { get; set; }

    public required DateTime CheckedAt { get; set; }
}
