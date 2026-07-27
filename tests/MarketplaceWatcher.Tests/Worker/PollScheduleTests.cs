using MarketplaceWatcher.Worker;
using Xunit;

namespace MarketplaceWatcher.Tests.Worker;

public class PollScheduleTests
{
    private static readonly TimeSpan Hourly = TimeSpan.FromHours(1);

    [Fact]
    public void Waits_until_the_top_of_the_next_hour()
    {
        var now = new DateTime(2026, 7, 28, 3, 17, 23);

        Assert.Equal(new TimeSpan(0, 42, 37), PollSchedule.NextDelay(now, Hourly));
        Assert.Equal(new DateTime(2026, 7, 28, 4, 0, 0), PollSchedule.NextPollAt(now, Hourly));
    }

    [Fact]
    public void Sitting_exactly_on_a_boundary_waits_a_full_interval()
    {
        // Otherwise a poll finishing at exactly 4:00:00.000 would immediately fire again.
        var now = new DateTime(2026, 7, 28, 4, 0, 0);

        Assert.Equal(Hourly, PollSchedule.NextDelay(now, Hourly));
    }

    [Fact]
    public void Does_not_drift_when_a_poll_takes_a_long_time()
    {
        // The 4:00 poll ran long and finished at 4:03. The next is still 5:00, not 5:03 —
        // that is the whole point of aligning to the clock instead of counting forward.
        var finishedLate = new DateTime(2026, 7, 28, 4, 3, 12);

        Assert.Equal(new DateTime(2026, 7, 28, 5, 0, 0), PollSchedule.NextPollAt(finishedLate, Hourly));
    }

    [Fact]
    public void Skipping_slots_lands_on_a_later_boundary_still_on_the_hour()
    {
        // How failure backoff is expressed: skip boundaries, stay aligned.
        var now = new DateTime(2026, 7, 28, 3, 17, 23);

        Assert.Equal(new DateTime(2026, 7, 28, 7, 0, 0), PollSchedule.NextPollAt(now, Hourly, slots: 4));
    }

    [Fact]
    public void Crosses_midnight_correctly()
    {
        var now = new DateTime(2026, 7, 28, 23, 40, 0);

        Assert.Equal(new DateTime(2026, 7, 29, 0, 0, 0), PollSchedule.NextPollAt(now, Hourly));
    }

    [Fact]
    public void Aligns_to_sub_hour_intervals_too()
    {
        // A 15-minute interval should land on :00/:15/:30/:45.
        var now = new DateTime(2026, 7, 28, 3, 17, 0);

        Assert.Equal(
            new DateTime(2026, 7, 28, 3, 30, 0),
            PollSchedule.NextPollAt(now, TimeSpan.FromMinutes(15)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_nonsensical_slot_count(int slots)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PollSchedule.NextDelay(DateTime.Now, Hourly, slots));
    }

    [Fact]
    public void Rejects_a_zero_interval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PollSchedule.NextDelay(DateTime.Now, TimeSpan.Zero));
    }
}
