namespace MarketplaceWatcher.Data;

public sealed class Seen
{
    public required string Signature { get; set; }
    public required string Title { get; set; }
    public required DateTimeOffset SeenAt { get; set; }
}
