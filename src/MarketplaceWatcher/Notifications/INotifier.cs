using MarketplaceWatcher.Domain;

namespace MarketplaceWatcher.Notifications;

/// <summary>
/// Sends an alert for a single genuinely-new listing.
/// Implementations must not throw — a failed notification must never stop the poll loop.
/// </summary>
public interface INotifier
{
    /// <param name="chatId">
    /// Who to alert. Null means the configured default recipient.
    /// </param>
    /// <returns>
    /// True if the alert was delivered. False means the caller must NOT mark the listing
    /// as seen, so it is retried on the next poll rather than lost.
    /// </returns>
    Task<bool> NotifyAsync(Listing listing, string watchName, string? chatId, CancellationToken cancellationToken);
}
