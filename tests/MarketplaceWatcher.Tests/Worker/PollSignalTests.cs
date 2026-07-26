using System.Diagnostics;
using MarketplaceWatcher.Worker;

namespace MarketplaceWatcher.Tests.Worker;

public class PollSignalTests
{
    [Fact]
    public async Task WaitAsync_ReturnsEarly_WhenTriggeredBeforeDelayElapses()
    {
        var signal = new PollSignal();
        var waitTask = signal.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        signal.Trigger();
        var stopwatch = Stopwatch.StartNew();
        await waitTask;
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WaitAsync_ReturnsAfterDelay_WhenNeverTriggered()
    {
        var signal = new PollSignal();

        var stopwatch = Stopwatch.StartNew();
        await signal.WaitAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(150));
    }
}
