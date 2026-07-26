using MarketplaceWatcher.Scraping;
using Xunit;

namespace MarketplaceWatcher.Tests.Scraping;

public class LocationFilterTests
{
    // Location strings below are real values seen on live cards.
    private static readonly string[] AucklandOnly = ["Auckland"];

    [Fact]
    public void Allows_everything_when_no_locations_are_configured()
    {
        // Fail open: an empty list means "filter disabled", never "reject everything".
        Assert.True(LocationFilter.IsAllowed("Tauranga, New Zealand", []));
    }

    [Fact]
    public void Allows_a_location_that_matches_the_configured_term()
    {
        Assert.True(LocationFilter.IsAllowed("Auckland, New Zealand", AucklandOnly));
    }

    [Theory]
    [InlineData("Tauranga, New Zealand")]
    [InlineData("Hamilton, New Zealand")]
    [InlineData("Napier, New Zealand")]
    public void Rejects_out_of_region_locations(string location)
    {
        Assert.False(LocationFilter.IsAllowed(location, AucklandOnly));
    }

    [Fact]
    public void Matches_case_insensitively()
    {
        Assert.True(LocationFilter.IsAllowed("auckland, new zealand", AucklandOnly));
    }

    [Fact]
    public void Allows_an_Auckland_suburb_once_it_is_listed_explicitly()
    {
        // "Takanini, New Zealand" is Auckland region but does not contain the word
        // "Auckland", so suburbs must be added by name. PollingWorker logs rejected
        // locations precisely so these can be spotted and added.
        string[] allowed = ["Auckland", "Takanini"];

        Assert.True(LocationFilter.IsAllowed("Takanini, New Zealand", allowed));
    }

    [Fact]
    public void Ignores_blank_entries_in_the_configured_list()
    {
        string[] allowed = ["", "   ", "Auckland"];

        Assert.True(LocationFilter.IsAllowed("Auckland, New Zealand", allowed));
        Assert.False(LocationFilter.IsAllowed("Napier, New Zealand", allowed));
    }

    [Fact]
    public void Rejects_a_blank_location_when_a_filter_is_configured()
    {
        Assert.False(LocationFilter.IsAllowed("", AucklandOnly));
    }
}
