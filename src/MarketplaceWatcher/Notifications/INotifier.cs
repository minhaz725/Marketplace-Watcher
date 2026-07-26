using MarketplaceWatcher.Domain;

namespace MarketplaceWatcher.Notifications;

/// <summary>
/// Sends an alert for a single genuinely-new listing.
/// Implementations must not throw — a failed notification must never stop the poll loop.
/// </summary>
public interface INotifier
{
    Task NotifyAsync(Listing listing, string watchName, CancellationToken cancellationToken);
}
