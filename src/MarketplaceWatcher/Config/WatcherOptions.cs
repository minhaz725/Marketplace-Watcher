namespace MarketplaceWatcher.Config;

public sealed class WatcherOptions
{
    public const string Section = "Watcher";
    public int PollIntervalSeconds { get; set; } = 90;
    public int DefaultRadiusKm { get; set; } = 5;
    public GeoPoint Location { get; set; } = new();

    /// <summary>
    /// Place names a listing's card location must contain (case-insensitive) to be alerted on.
    /// Empty disables the filter. Add Auckland suburbs here as the log surfaces them.
    /// </summary>
    public string[] AllowedLocations { get; set; } = [];
}

public sealed class GeoPoint
{
    public double Lat { get; set; } = -36.8485;
    public double Lng { get; set; } = 174.7633;
}
