namespace MarketplaceWatcher.Scraping;

/// <summary>
/// Decides when a virtualized-feed scroll-harvest has actually finished.
/// Pure logic, deliberately separated from the Playwright IO so it can be unit tested.
/// </summary>
public sealed class HarvestPolicy
{
    private readonly int _maxRounds;
    private readonly int _stagnantRoundsBeforeStop;

    public HarvestPolicy(int maxRounds = 12, int stagnantRoundsBeforeStop = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRounds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(stagnantRoundsBeforeStop, 1);

        _maxRounds = maxRounds;
        _stagnantRoundsBeforeStop = stagnantRoundsBeforeStop;
    }

    /// <param name="roundsCompleted">How many harvest-then-scroll rounds have run.</param>
    /// <param name="consecutiveStagnantRounds">
    /// How many rounds in a row produced no listing id we had not already seen.
    /// </param>
    public bool ShouldContinue(int roundsCompleted, int consecutiveStagnantRounds) =>
        roundsCompleted < _maxRounds &&
        consecutiveStagnantRounds < _stagnantRoundsBeforeStop;

    /// <summary>
    /// True when the harvest stopped because it ran out of rounds rather than because the
    /// feed was exhausted — i.e. listings were almost certainly left unharvested. Callers
    /// should log this: a truncated harvest silently looks identical to a complete one.
    /// </summary>
    public bool StoppedEarly(int roundsCompleted, int consecutiveStagnantRounds) =>
        roundsCompleted >= _maxRounds &&
        consecutiveStagnantRounds < _stagnantRoundsBeforeStop;
}
