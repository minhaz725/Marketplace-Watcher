namespace MarketplaceWatcher.Worker;

/// <summary>
/// Works out how long to wait for the next poll, snapped to the clock rather than counting
/// forward from whenever the last one finished. With an hourly interval that means 4:00,
/// 5:00, 6:00 — not 4:00, 5:03, 6:07 drifting by however long each scrape took.
///
/// Pure so it can be unit tested: the caller passes "now" instead of this reading the clock.
/// </summary>
public static class PollSchedule
{
    /// <param name="now">Local time.</param>
    /// <param name="interval">Gap between polls, e.g. one hour.</param>
    /// <param name="slots">
    /// How many boundaries to wait for. 1 is the next one; higher values skip boundaries,
    /// which is how failure backoff is expressed on an aligned schedule.
    /// </param>
    public static TimeSpan NextDelay(DateTime now, TimeSpan interval, int slots = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(slots, 1);

        // Boundaries are measured from midnight, so an hourly interval lands on the hour and
        // a 15-minute one on :00/:15/:30/:45.
        var sinceMidnight = now.TimeOfDay;
        var intervalTicks = interval.Ticks;
        var elapsedInCurrentSlot = sinceMidnight.Ticks % intervalTicks;

        // Landing exactly on a boundary waits a full interval rather than firing twice.
        var toNextBoundary = intervalTicks - elapsedInCurrentSlot;

        return TimeSpan.FromTicks(toNextBoundary + (slots - 1) * intervalTicks);
    }

    /// <summary>The wall-clock time a delay resolves to — for logging what happens next.</summary>
    public static DateTime NextPollAt(DateTime now, TimeSpan interval, int slots = 1) =>
        now + NextDelay(now, interval, slots);
}
