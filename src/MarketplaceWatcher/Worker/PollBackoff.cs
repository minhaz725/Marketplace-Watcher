namespace MarketplaceWatcher.Worker;

/// <summary>
/// How much to stretch the poll interval after consecutive failed polls, so a persistently
/// broken dependency (Chrome closed, Facebook unreachable) is retried gently instead of
/// hammered every 90 seconds.
/// </summary>
public static class PollBackoff
{
    private const int MaxMultiplier = 8;

    public static int Multiplier(int consecutiveFailures) =>
        consecutiveFailures <= 0
            ? 1
            : Math.Min(1 << Math.Min(consecutiveFailures, 10), MaxMultiplier);
}
