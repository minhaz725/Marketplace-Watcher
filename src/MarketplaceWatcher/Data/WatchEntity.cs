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
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
