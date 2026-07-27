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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseInterval = options.Value.PollIntervalSeconds;
        var allowedLocations = options.Value.AllowedLocations;

        logger.LogInformation(
            allowedLocations.Length > 0
                ? "Location filter active — only listings matching: {Locations}"
                : "Location filter disabled — listings from anywhere will be alerted",
            string.Join(", ", allowedLocations));

        var consecutiveFailures = 0;

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

                        var matched = local
                            .Where(card => WatchMatcher.Matches(card, watch))
                            .Select(card => new Listing(
                                card.Title,
                                card.Price,
                                card.Location,
                                $"https://www.facebook.com/marketplace/item/{card.ListingId}/"))
                            .ToList();

                        var newForWatch = await db.GetNewAsync(matched, stoppingToken);
                        var delivered = new List<Listing>();

                        foreach (var listing in newForWatch)
                        {
                            logger.LogInformation(
                                "MATCHED [{Watch}]: {Title} (${Price}) - {Url}",
                                watchName,
                                listing.Title,
                                listing.Price,
                                listing.Url);

                            if (await notifier.NotifyAsync(listing, watchName, stoppingToken))
                            {
                                delivered.Add(listing);
                            }
                        }

                        // Only delivered alerts are recorded. Anything that failed to send stays
                        // unseen and is retried next poll — at-least-once rather than the old
                        // at-most-once, where a Telegram outage lost alerts permanently.
                        await db.MarkSeenAsync(delivered, stoppingToken);

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

            var jitteredSeconds = Random.Shared.Next(baseInterval - 30, baseInterval + 30);
            var multiplier = PollBackoff.Multiplier(consecutiveFailures);
            if (multiplier > 1)
            {
                logger.LogWarning(
                    "{Failures} consecutive failed poll(s); backing off to {Seconds}s",
                    consecutiveFailures, jitteredSeconds * multiplier);
            }

            await pollSignal.WaitAsync(TimeSpan.FromSeconds(jitteredSeconds * multiplier), stoppingToken);
        }
    }
}
