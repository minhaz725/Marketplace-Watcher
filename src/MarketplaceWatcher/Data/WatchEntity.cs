namespace MarketplaceWatcher.Data;

public sealed class WatchEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool FreeOnly { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? RadiusKm { get; set; }
    public string[] MustIncludeKeywords { get; set; } = [];
    public string[] ExcludeKeywords { get; set; } = [];
    public string? Condition { get; set; }
    public string? Category { get; set; }
    /// <summary>
    /// Only scrape listings posted within this many days. Null falls back to
    /// <c>Watcher:DaysSinceListed</c> from config (default 1). Widen it for slow-moving
    /// searches like cars, where a good listing can sit for a week.
    /// </summary>
    public int? DaysSinceListed { get; set; }

    /// <summary>
    /// Telegram chat to alert for this watch. Null falls back to <c>Telegram:ChatId</c> from
    /// config, so existing watches keep working unchanged.
    /// </summary>
    public string? ChatId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
