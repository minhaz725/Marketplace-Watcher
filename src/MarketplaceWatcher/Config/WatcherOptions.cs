namespace MarketplaceWatcher.Config;

public sealed class WatcherOptions
{
    public const string Section = "Watcher";
    public int PollIntervalSeconds { get; set; } = 90;
    public int DefaultRadiusKm { get; set; } = 5;
    public GeoPoint Location { get; set; } = new();
    public List<Watch> Watches { get; set; } = [];
}

public sealed class Watch
{
    public string? Name { get; set; }
    public bool FreeOnly { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? RadiusKm { get; set; }
    public string[] MustIncludeKeywords { get; set; } = [];
    public string[] ExcludeKeywords { get; set; } = [];
    public string? Condition { get; set; }
    public string? Category { get; set; }
}

public sealed class GeoPoint
{
    public double Lat { get; set; } = -36.8485;
    public double Lng { get; set; } = 174.7633;
}
