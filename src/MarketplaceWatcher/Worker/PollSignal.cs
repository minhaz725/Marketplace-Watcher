using System.Threading.Channels;

namespace MarketplaceWatcher.Worker;

// Lets the API wake the poll loop early (e.g. right after a new watch is created)
// instead of waiting out the full jittered delay. Singleton, shared between the
// PollingWorker and any request handler that wants to nudge it.
public sealed class PollSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Trigger() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        var delayTask = Task.Delay(delay, cancellationToken);
        var signalTask = _channel.Reader.WaitToReadAsync(cancellationToken).AsTask();
        await Task.WhenAny(delayTask, signalTask);
        _channel.Reader.TryRead(out _);
    }
}
