namespace MarketplaceWatcher.Config;

public sealed class WatcherOptions
{
    public const string Section = "Watcher";
    public int PollIntervalSeconds { get; set; } = 90;
    public int DefaultRadiusKm { get; set; } = 5;
    public GeoPoint Location { get; set; } = new();
}

public sealed class GeoPoint
{
    public double Lat { get; set; } = -36.8485;
    public double Lng { get; set; } = 174.7633;
}
