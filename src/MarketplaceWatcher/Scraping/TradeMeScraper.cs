using MarketplaceWatcher.Data;

namespace MarketplaceWatcher.Scraping;

public sealed class TradeMeScraper(
    ILogger<TradeMeScraper> logger,
    CdpConnectionProvider cdpProvider) : ITradeMeScraper
{
    private const string CardSelector = "a[href*='/listing/']";
    private const int FirstCardTimeoutMs = 20_000;

    public async Task<IReadOnlyList<TradeMeCard>> ScrapeAsync(WatchEntity watch, CancellationToken cancellationToken = default)
    {
        var url = TradeMeSearchUrlBuilder.Build(watch);
        if (url is null)
        {
            logger.LogWarning("Watch {Name} is not a make-and-model search; skipping Trade Me", watch.Name);
            return [];
        }

        var cdp = await cdpProvider.GetAsync();
        var page = await cdp.Context.NewPageAsync();
        try
        {
            logger.LogInformation("Scraping {Url} for watch {Name}", url, watch.Name);
            await page.GotoAsync(url);

            try
            {
                await page.WaitForSelectorAsync(CardSelector, new() { Timeout = FirstCardTimeoutMs });
            }
            catch (TimeoutException)
            {
                // A model with no listings in range is a normal outcome, not an error.
                logger.LogInformation("No Trade Me listings appeared for {Url}", url);
                return [];
            }

            // [href, innerText] pairs. Plain arrays rather than an object, so nothing depends on
            // how Playwright maps JSON property names onto a C# type.
            var raw = await page.EvalOnSelectorAllAsync<string?[][]>(
                CardSelector,
                "els => els.map(e => [e.getAttribute('href'), e.innerText])");

            // The same car can appear twice (e.g. featured and organic), so dedupe on id.
            var cards = raw
                .Select(r => TradeMeCardParser.Parse(r[0] ?? string.Empty, r[1] ?? string.Empty))
                .OfType<TradeMeCard>()
                .DistinctBy(c => c.ListingId)
                .ToList();

            logger.LogInformation("Read {Count} Trade Me card(s) for watch {Name}", cards.Count, watch.Name);
            return cards;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async Task<(DateTime? Rego, DateTime? Wof)> FetchExpiryAsync(string listingUrl, CancellationToken cancellationToken = default)
    {
        var cdp = await cdpProvider.GetAsync();
        var page = await cdp.Context.NewPageAsync();
        try
        {
            await page.GotoAsync(listingUrl);
            await page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

            // The expiry fields render after the shell, so wait for their label (best effort:
            // a listing with no dates simply never shows it, and that is a valid result).
            try
            {
                await page.WaitForSelectorAsync("text=/Registration expires|WoF expires/i", new() { Timeout = 8_000 });
            }
            catch (TimeoutException)
            {
            }

            return TradeMeExpiryParser.Parse(await page.InnerTextAsync("body"));
        }
        catch (Exception ex)
        {
            // A failed read must not abort the poll. The caller caches "unknown" so it is not retried forever.
            logger.LogWarning("Could not read WOF/rego for {Url}: {Message}", listingUrl, ex.Message);
            return (null, null);
        }
        finally
        {
            await page.CloseAsync();
        }
    }
}
