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

    /// <summary>
    /// Only scrape listings posted within this many days (Marketplace's own "Date listed"
    /// filter). Null or 0 disables it and scrapes the whole feed.
    /// </summary>
    public int? DaysSinceListed { get; set; } = 1;
}

public sealed class GeoPoint
{
    public double Lat { get; set; } = -36.8485;
    public double Lng { get; set; } = 174.7633;
}
