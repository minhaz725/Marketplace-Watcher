using MarketplaceWatcher.Config;
using MarketplaceWatcher.Data;
using MarketplaceWatcher.Domain;
using MarketplaceWatcher.Notifications;
using MarketplaceWatcher.Scraping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MarketplaceWatcher.Worker;

/// <summary>
/// Polls Trade Me Motors for one watch: scrape, card filters, drop what was already alerted,
/// then read WOF/rego for the rest (cached) and alert. Kept out of PollingWorker so that
/// class stays about scheduling and failure handling.
/// </summary>
public sealed class TradeMePoller(
    ILogger<TradeMePoller> logger,
    IOptions<WatcherOptions> options,
    ITradeMeScraper scraper)
{
    /// <summary>Listing pages opened per watch per poll, so a first run spreads over a few cycles.</summary>
    private const int MaxExpiryFetchesPerPoll = 10;

    /// <summary>CheckedListings keys are shared with Facebook ids, so Trade Me ids are prefixed.</summary>
    private static string CacheKey(string listingId) => $"tm:{listingId}";

    /// <returns>The listings whose alert was delivered.</returns>
    public async Task<List<Listing>> PollWatchAsync(
        WatchEntity watch,
        WatcherDbContext db,
        INotifier notifier,
        CancellationToken cancellationToken)
    {
        var watchName = watch.Name ?? "(unnamed)";
        var cards = await scraper.ScrapeAsync(watch, cancellationToken);

        var allowed = options.Value.AllowedLocations;
        var local = cards.Where(c => LocationFilter.IsAllowed(c.Location, allowed)).ToList();
        var matched = local.Where(c => TradeMeMatcher.Matches(c, watch)).ToList();

        // Already-alerted cars are dropped BEFORE any page is opened.
        var byUrl = matched.ToDictionary(c => c.Url);
        var newListings = await db.GetNewAsync(
            matched.Select(c => Build(c, watch, note: null)),
            cancellationToken);

        var delivered = new List<Listing>();
        var fetched = 0;
        var rejectedOnExpiry = 0;

        foreach (var candidate in newListings)
        {
            var card = byUrl[candidate.Url];
            var cacheKey = CacheKey(card.ListingId);

            var cached = await db.CheckedListings.FirstOrDefaultAsync(c => c.ListingId == cacheKey, cancellationToken);
            DateTime? rego;
            DateTime? wof;

            if (cached is not null)
            {
                (rego, wof) = (cached.RegoExpires, cached.WofExpires);
            }
            else if (watch.MinExpiryMonths is null)
            {
                (rego, wof) = (null, null); // No expiry rule on this watch, so no page load.
            }
            else if (fetched >= MaxExpiryFetchesPerPoll)
            {
                continue; // Try again next poll rather than exceeding the budget.
            }
            else
            {
                (rego, wof) = await scraper.FetchExpiryAsync(card.Url, cancellationToken);
                fetched++;

                // Cached either way: a rejected car is never written to Seen, so without this it
                // would be re-opened on every poll.
                db.CheckedListings.Add(new CheckedListing
                {
                    ListingId = cacheKey,
                    RegoExpires = rego,
                    WofExpires = wof,
                    CheckedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync(cancellationToken);
            }

            if (watch.MinExpiryMonths is { } months &&
                !(TradeMeExpiryRule.Passes(rego, DateTime.Today, months) &&
                  TradeMeExpiryRule.Passes(wof, DateTime.Today, months)))
            {
                rejectedOnExpiry++;
                continue;
            }

            var listing = Build(card, watch, DescribeCar(card, rego, wof));

            logger.LogInformation("MATCHED [{Watch}] (Trade Me): {Title} (${Price}) - {Url}",
                watchName, listing.Title, listing.Price, listing.Url);

            if (await notifier.NotifyAsync(listing, watchName, watch.ChatId, cancellationToken))
            {
                delivered.Add(listing);
            }
        }

        // Only delivered alerts are recorded, so a failed send is retried next poll.
        await db.MarkSeenAsync(delivered, watch.Id, watchName, cancellationToken);

        logger.LogInformation(
            "Trade Me {Name} funnel: {Cards} cards → {Local} in-region → {Matched} passed filters → "
            + "{New} not seen before → {Expiry} failed WOF/rego → {Delivered} alerted ({Fetched} page load(s))",
            watchName, cards.Count, local.Count, matched.Count, newListings.Count,
            rejectedOnExpiry, delivered.Count, fetched);

        return delivered;
    }

    private static Listing Build(TradeMeCard card, WatchEntity watch, string? note) =>
        new(card.Title, card.Price, card.Location, card.Url, ListingSources.TradeMe, note);

    private static string DescribeCar(TradeMeCard card, DateTime? rego, DateTime? wof)
    {
        var km = card.OdometerKm is { } k ? $"{k:N0} km" : "odometer not shown";
        var parts = new List<string>
        {
            km,
            $"Rego {Month(rego)}",
            $"WoF {Month(wof)}",
        };

        if (card.OdometerKm is null || rego is null || wof is null)
        {
            parts.Add("⚠ check the listing");
        }

        return string.Join(" · ", parts);
    }

    private static string Month(DateTime? date) => date is { } d ? d.ToString("MMM yyyy") : "not shown";
}
