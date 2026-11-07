using MarketplaceWatcher.Config;
using MarketplaceWatcher.Data;
using MarketplaceWatcher.Domain;
using MarketplaceWatcher.Notifications;
using MarketplaceWatcher.Scraping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MarketplaceWatcher.Worker;

public class PollingWorker(
    ILogger<PollingWorker> logger,
    IOptions<WatcherOptions> options,
    IServiceScopeFactory scopeFactory,
    IMarketplaceScraper scraper,
    PollSignal pollSignal,
    WatcherStatus status) : BackgroundService
{
    // A first poll on a fresh car watch can find dozens of titleless listings. Cap the page
    // loads per poll so it spreads over a few cycles instead of hammering Facebook once.
    private const int MaxEnrichmentsPerPoll = 10;

    /// <summary>Ceiling on failure backoff, so a broken dependency is still retried today.</summary>
    private static readonly TimeSpan MaxBackoffDelay = TimeSpan.FromHours(4);

    /// <summary>
    /// Replaces titleless cards with title-bearing ones where we can. Cards whose title
    /// cannot be recovered are dropped (ADR-005).
    /// </summary>
    private async Task<List<ScrapedCard>> EnrichTitlelessAsync(
        List<ScrapedCard> cards,
        Data.WatchEntity watch,
        WatcherDbContext db,
        CancellationToken cancellationToken)
    {
        var result = cards.Where(c => c.HasTitle).ToList();

        // Only worth a page load if the price already fits — no point fetching a car that is
        // out of budget regardless of what it turns out to be called.
        var candidates = cards
            .Where(c => !c.HasTitle && WatchMatcher.MatchesPrice(c, watch))
            .ToList();

        if (candidates.Count == 0)
        {
            return result;
        }

        var ids = candidates.Select(c => c.ListingId).ToList();
        var cached = await db.CheckedListings
            .Where(c => ids.Contains(c.ListingId))
            .ToDictionaryAsync(c => c.ListingId, c => c.Title, cancellationToken);

        var fetched = 0;
        var recovered = 0;

        foreach (var card in candidates)
        {
            string? title;

            if (cached.TryGetValue(card.ListingId, out var cachedTitle))
            {
                title = cachedTitle;
            }
            else if (fetched >= MaxEnrichmentsPerPoll)
            {
                continue; // Try again next poll rather than exceeding the budget.
            }
            else
            {
                title = await scraper.FetchTitleAsync(card.ListingId, cancellationToken);
                fetched++;

                // Cached either way — a null result must not be re-fetched every poll.
                db.CheckedListings.Add(new Data.CheckedListing
                {
                    ListingId = card.ListingId,
                    Title = title,
                    CheckedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync(cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                result.Add(card with { Title = title });
                recovered++;
            }
        }

        if (candidates.Count > 0)
        {
            logger.LogInformation(
                "Watch {Name}: {Candidates} titleless in-budget listing(s), {Fetched} page load(s), "
                + "{Recovered} title(s) recovered",
                watch.Name ?? "(free-only / match-all)", candidates.Count, fetched, recovered);
        }

        return result;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
        var allowedLocations = options.Value.AllowedLocations;

        if (options.Value.PollOnStartup)
        {
            logger.LogInformation(
                "Polling every {Interval}, aligned to the clock. First poll now, then {NextPoll:HH:mm}.",
                interval, PollSchedule.NextPollAt(DateTime.Now, interval));
        }
        else
        {
            logger.LogInformation(
                "Polling every {Interval}, aligned to the clock. Watcher:PollOnStartup is off — "
                + "no poll at startup; first scheduled poll {NextPoll:HH:mm} (or click Poll now).",
                interval, PollSchedule.NextPollAt(DateTime.Now, interval));
        }

        logger.LogInformation(
            allowedLocations.Length > 0
                ? "Location filter active — only listings matching: {Locations}"
                : "Location filter disabled — listings from anywhere will be alerted",
            string.Join(", ", allowedLocations));

        var consecutiveFailures = 0;

        if (!options.Value.PollOnStartup)
        {
            // Wait out the slot exactly as the end of a normal iteration does, so "Poll now"
            // (PollSignal) still wakes it early.
            await pollSignal.WaitAsync(PollSchedule.NextDelay(DateTime.Now, interval), stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var pollFailed = false;
            var lastError = "";
            status.RecordPollStarted(DateTimeOffset.UtcNow);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WatcherDbContext>();

                // Resolved per iteration, not injected into this singleton's constructor:
                // typed HttpClients are transient, and holding one for the app's lifetime
                // would defeat IHttpClientFactory's handler rotation.
                var notifier = scope.ServiceProvider.GetRequiredService<INotifier>();

                var watches = await db.Watches.Where(w => w.IsActive).ToListAsync(stoppingToken);
                logger.LogInformation(
                    "{Count} active watch(es): {Names}",
                    watches.Count,
                    string.Join(", ", watches.Select(w => w.Name ?? "(free-only / match-all)")));

                var allNew = new List<Listing>();

                // Sequential, one watch (one page) at a time — CLAUDE.md section 5's
                // "gentle scraping" rule, not just a Task.WhenAll-avoidance habit.
                foreach (var watch in watches)
                {
                    var watchName = watch.Name ?? "(free-only / match-all)";

                    try
                    {
                        var cards = await scraper.ScrapeAsync(watch, stoppingToken);

                        // Facebook pads sparse searches with out-of-region listings, so the
                        // card's own location is filtered before anything else looks at it.
                        var local = cards
                            .Where(card => LocationFilter.IsAllowed(card.Location, allowedLocations))
                            .ToList();

                        if (local.Count != cards.Count)
                        {
                            var rejected = cards
                                .Where(card => !LocationFilter.IsAllowed(card.Location, allowedLocations))
                                .Select(card => card.Location)
                                .Distinct();

                            // Logged, not silent: this is how you spot an Auckland suburb that
                            // needs adding to AllowedLocations rather than losing it unnoticed.
                            logger.LogInformation(
                                "Dropped {Count} out-of-region listing(s) for watch {Name}: {Locations}",
                                cards.Count - local.Count,
                                watch.Name ?? "(free-only / match-all)",
                                string.Join(", ", rejected));
                        }

                        // Titleless cards can't be keyword-filtered, so recover the title from
                        // the listing's own page — but only for ones whose price already fits,
                        // and only once each ever (CheckedListings is the cache).
                        var enriched = await EnrichTitlelessAsync(local, watch, db, stoppingToken);

                        var matched = enriched
                            .Where(card => WatchMatcher.Matches(card, watch))
                            .Select(card => new Listing(
                                card.Title,
                                card.Price,
                                card.Location,
                                $"https://www.facebook.com/marketplace/item/{card.ListingId}/"))
                            .ToList();

                        var newForWatch = await db.GetNewAsync(matched, stoppingToken);

                        // Where did the listings go? Without this, "42 harvested, 0 alerts"
                        // gives no clue whether the location filter, the price band or an
                        // exclude keyword ate them.
                        logger.LogInformation(
                            "Watch {Name} funnel: {Harvested} harvested ({Titleless} with no title) → "
                            + "{Local} in-region → {Matched} passed filters → {New} not seen before",
                            watchName, cards.Count, cards.Count(c => !c.HasTitle),
                            local.Count, matched.Count, newForWatch.Count);

                        var delivered = new List<Listing>();

                        foreach (var listing in newForWatch)
                        {
                            logger.LogInformation(
                                "MATCHED [{Watch}]: {Title} (${Price}) - {Url}",
                                watchName,
                                listing.Title,
                                listing.Price,
                                listing.Url);

                            if (await notifier.NotifyAsync(listing, watchName, watch.ChatId, stoppingToken))
                            {
                                delivered.Add(listing);
                            }
                        }

                        // Only delivered alerts are recorded. Anything that failed to send stays
                        // unseen and is retried next poll — at-least-once rather than the old
                        // at-most-once, where a Telegram outage lost alerts permanently.
                        await db.MarkSeenAsync(delivered, watch.Id, watchName, stoppingToken);

                        if (delivered.Count != newForWatch.Count)
                        {
                            logger.LogWarning(
                                "{Failed} of {Total} alert(s) for watch {Name} failed to send; "
                                + "left unseen and will be retried next poll.",
                                newForWatch.Count - delivered.Count, newForWatch.Count, watchName);
                        }

                        allNew.AddRange(delivered);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // One broken watch (Playwright timeout, DOM change, bad query) must
                        // not abort the other watches or the loop.
                        pollFailed = true;
                        lastError = $"watch '{watchName}': {ex.Message}";
                        logger.LogError(ex, "Watch {Name} failed this poll; continuing", watchName);
                    }
                }

                logger.LogInformation("polled, {Count} new", allNew.Count);
                status.RecordPollSucceeded(DateTimeOffset.UtcNow, allNew.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Previously an unhandled exception here killed the entire host
                // (BackgroundServiceExceptionBehavior.StopHost is the default), so the
                // watcher stopped silently instead of degrading.
                pollFailed = true;
                lastError = ex.Message;
                logger.LogError(ex, "Poll iteration failed; the watcher will keep running");
            }

            consecutiveFailures = pollFailed ? consecutiveFailures + 1 : 0;
            status.RecordFailures(consecutiveFailures, lastError);

            // Failure backoff skips whole slots rather than adding an offset, so a retry
            // still lands on the hour. Capped separately below.
            var slots = PollBackoff.Multiplier(consecutiveFailures);
            var delay = PollSchedule.NextDelay(DateTime.Now, interval, slots);

            // With an hourly interval the 8x backoff would mean an 8-hour blackout, which is
            // too long to notice a problem. Cap the wait itself rather than the slot count,
            // so the same rule works whatever the interval is.
            if (delay > MaxBackoffDelay)
            {
                delay = PollSchedule.NextDelay(DateTime.Now, interval);
                slots = 1;
            }

            if (slots > 1)
            {
                logger.LogWarning(
                    "{Failures} consecutive failed poll(s); skipping to {NextPoll:HH:mm}",
                    consecutiveFailures, DateTime.Now + delay);
            }
            else
            {
                logger.LogInformation("Next poll at {NextPoll:HH:mm}", DateTime.Now + delay);
            }

            await pollSignal.WaitAsync(delay, stoppingToken);
        }
    }
}
