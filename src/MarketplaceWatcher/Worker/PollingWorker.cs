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
    PollSignal pollSignal) : BackgroundService
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

        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = scopeFactory.CreateScope())
            {
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
                    var watchName = watch.Name ?? "(free-only / match-all)";
                    foreach (var listing in newForWatch)
                    {
                        logger.LogInformation(
                            "MATCHED [{Watch}]: {Title} (${Price}) - {Url}",
                            watchName,
                            listing.Title,
                            listing.Price,
                            listing.Url);

                        await notifier.NotifyAsync(listing, watchName, stoppingToken);
                    }

                    allNew.AddRange(newForWatch);
                }

                logger.LogInformation("polled, {Count} new", allNew.Count);
            }

            var jitteredSeconds = Random.Shared.Next(baseInterval - 30, baseInterval + 30);
            await pollSignal.WaitAsync(TimeSpan.FromSeconds(jitteredSeconds), stoppingToken);
        }
    }
}
