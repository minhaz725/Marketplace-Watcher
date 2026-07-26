using MarketplaceWatcher.Scraping;
using Xunit;

namespace MarketplaceWatcher.Tests.Scraping;

public class HarvestPolicyTests
{
    [Fact]
    public void Continues_before_anything_has_been_harvested()
    {
        var policy = new HarvestPolicy(maxRounds: 12, stagnantRoundsBeforeStop: 2);

        Assert.True(policy.ShouldContinue(roundsCompleted: 0, consecutiveStagnantRounds: 0));
    }

    [Fact]
    public void Keeps_going_while_new_cards_are_still_arriving()
    {
        var policy = new HarvestPolicy(maxRounds: 12, stagnantRoundsBeforeStop: 2);

        Assert.True(policy.ShouldContinue(roundsCompleted: 8, consecutiveStagnantRounds: 0));
    }

    [Fact]
    public void A_single_stagnant_round_is_not_enough_to_stop()
    {
        // This is the whole point of the policy. Phase 10 recon showed the same URL
        // yielding 23 then 46 listings — one slow lazy-load round must not be mistaken
        // for "the feed is exhausted", or the poll silently misses half the listings.
        var policy = new HarvestPolicy(maxRounds: 12, stagnantRoundsBeforeStop: 2);

        Assert.True(policy.ShouldContinue(roundsCompleted: 3, consecutiveStagnantRounds: 1));
    }

    [Fact]
    public void Stops_once_enough_consecutive_rounds_find_nothing_new()
    {
        var policy = new HarvestPolicy(maxRounds: 12, stagnantRoundsBeforeStop: 2);

        Assert.False(policy.ShouldContinue(roundsCompleted: 3, consecutiveStagnantRounds: 2));
    }

    [Fact]
    public void Stops_at_the_round_cap_even_if_cards_keep_arriving()
    {
        // Safety net: an endlessly-scrolling feed must not hold the poll loop forever
        // (CLAUDE.md section 5 — gentle scraping).
        var policy = new HarvestPolicy(maxRounds: 12, stagnantRoundsBeforeStop: 2);

        Assert.False(policy.ShouldContinue(roundsCompleted: 12, consecutiveStagnantRounds: 0));
    }

    [Fact]
    public void Reports_a_truncated_harvest_when_the_round_cap_ends_it()
    {
        var policy = new HarvestPolicy(maxRounds: 12, stagnantRoundsBeforeStop: 2);

        Assert.True(policy.StoppedEarly(roundsCompleted: 12, consecutiveStagnantRounds: 0));
    }

    [Fact]
    public void Does_not_report_truncation_when_the_feed_ran_dry()
    {
        // Stagnation and the cap can coincide; exhaustion is the honest explanation.
        var policy = new HarvestPolicy(maxRounds: 12, stagnantRoundsBeforeStop: 2);

        Assert.False(policy.StoppedEarly(roundsCompleted: 12, consecutiveStagnantRounds: 2));
        Assert.False(policy.StoppedEarly(roundsCompleted: 4, consecutiveStagnantRounds: 2));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(12, 0)]
    public void Rejects_nonsensical_configuration(int maxRounds, int stagnantRoundsBeforeStop)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HarvestPolicy(maxRounds, stagnantRoundsBeforeStop));
    }
}
