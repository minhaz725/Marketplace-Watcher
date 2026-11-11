using MarketplaceWatcher.Scraping;

namespace MarketplaceWatcher.Tests.Scraping;

public class TradeMeExpiryTests
{
    [Fact]
    public void Reads_rego_and_wof_as_the_last_day_of_the_month_shown()
    {
        var body = "Number of owners:\n2 owners\nImport history:\nImported\nRegistration expires:\nMar 2027\nWoF expires:\nOct 2027\nRatings";

        var (rego, wof) = TradeMeExpiryParser.Parse(body);

        Assert.Equal(new DateTime(2027, 3, 31), rego);
        Assert.Equal(new DateTime(2027, 10, 31), wof);
    }

    [Fact]
    public void A_missing_field_comes_back_null_not_zero()
    {
        var (rego, wof) = TradeMeExpiryParser.Parse("Registration expires:\nMar 2027\nRatings");

        Assert.NotNull(rego);
        Assert.Null(wof);
    }

    [Fact]
    public void Nothing_found_gives_two_nulls()
    {
        var (rego, wof) = TradeMeExpiryParser.Parse("just a description");

        Assert.Null(rego);
        Assert.Null(wof);
    }

    // "Today" is 2026-11-08; 4 months out is 2027-03-08.
    [Theory]
    [InlineData("2027-03-31", true)]   // borderline month passes — only the month is known
    [InlineData("2027-10-31", true)]
    [InlineData("2027-02-28", false)]  // ends before the cutoff
    [InlineData("2026-12-31", false)]
    public void Passes_only_when_expiry_is_beyond_the_minimum_months(string expiry, bool expected)
    {
        var today = new DateTime(2026, 11, 8);

        Assert.Equal(expected, TradeMeExpiryRule.Passes(DateTime.Parse(expiry), today, 4));
    }

    [Fact]
    public void An_unknown_date_passes_so_you_can_check_it_yourself()
    {
        Assert.True(TradeMeExpiryRule.Passes(null, new DateTime(2026, 11, 8), 4));
    }
}
