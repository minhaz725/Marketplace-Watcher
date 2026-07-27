namespace MarketplaceWatcher.Worker;

/// <summary>
/// Live state of the poll loop, written by <see cref="PollingWorker"/> and read by
/// GET /api/status. Registered as a singleton so both sides share one instance.
///
/// Every field is written from the single poll loop and read from arbitrary request
/// threads, so each uses Interlocked/volatile-style access rather than a lock: the
/// reader only ever needs a coherent snapshot of individual values, never a
/// transaction across all of them.
/// </summary>
public sealed class WatcherStatus
{
    private long _lastPollTicks;
    private long _lastSuccessfulPollTicks;
    private int _lastPollNewCount;
    private int _totalAlertsSent;
    private int _consecutiveFailures;
    private string _lastError = "";

    public DateTimeOffset? LastPollAt => Read(ref _lastPollTicks);

    public DateTimeOffset? LastSuccessfulPollAt => Read(ref _lastSuccessfulPollTicks);

    public int LastPollNewCount => Volatile.Read(ref _lastPollNewCount);

    public int TotalAlertsSent => Volatile.Read(ref _totalAlertsSent);

    public int ConsecutiveFailures => Volatile.Read(ref _consecutiveFailures);

    public string LastError => Volatile.Read(ref _lastError);

    public bool IsHealthy => ConsecutiveFailures == 0;

    public void RecordPollStarted(DateTimeOffset at) =>
        Interlocked.Exchange(ref _lastPollTicks, at.ToUnixTimeMilliseconds());

    public void RecordPollSucceeded(DateTimeOffset at, int newCount)
    {
        Interlocked.Exchange(ref _lastSuccessfulPollTicks, at.ToUnixTimeMilliseconds());
        Volatile.Write(ref _lastPollNewCount, newCount);
        Interlocked.Add(ref _totalAlertsSent, newCount);
        Volatile.Write(ref _lastError, "");
    }

    public void RecordFailures(int consecutiveFailures, string lastError)
    {
        Volatile.Write(ref _consecutiveFailures, consecutiveFailures);
        Volatile.Write(ref _lastError, lastError);
    }

    private static DateTimeOffset? Read(ref long ticks)
    {
        var value = Interlocked.Read(ref ticks);
        return value == 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(value);
    }
}
