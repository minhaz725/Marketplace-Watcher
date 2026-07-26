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
                    var matched = cards
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
