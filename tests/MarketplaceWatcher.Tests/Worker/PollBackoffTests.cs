using MarketplaceWatcher.Worker;
using Xunit;

namespace MarketplaceWatcher.Tests.Worker;

public class PollBackoffTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    public void Doubles_the_interval_per_consecutive_failure(int failures, int expected)
    {
        Assert.Equal(expected, PollBackoff.Multiplier(failures));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void Caps_the_multiplier_so_polling_never_stops_entirely(int failures)
    {
        // A long outage must still retry roughly every 12 minutes, not drift to hours.
        // int.MaxValue also guards the shift operator against overflowing to nonsense.
        Assert.Equal(8, PollBackoff.Multiplier(failures));
    }

    [Fact]
    public void Treats_a_healthy_loop_as_no_backoff()
    {
        Assert.Equal(1, PollBackoff.Multiplier(0));
        Assert.Equal(1, PollBackoff.Multiplier(-1));
    }
}
