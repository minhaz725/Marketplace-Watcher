using MarketplaceWatcher.Config;
using MarketplaceWatcher.Data;
using MarketplaceWatcher.Domain;
using MarketplaceWatcher.Scraping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MarketplaceWatcher.Worker;

public class PollingWorker(
    ILogger<PollingWorker> logger,
    IOptions<WatcherOptions> options,
    IConfiguration config,
    IServiceScopeFactory scopeFactory,
    IMarketplaceScraper scraper,
    PollSignal pollSignal) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var botTokenLoaded = !string.IsNullOrEmpty(config["Telegram:BotToken"]);
        logger.LogInformation("Telegram bot token loaded from config: {Loaded}", botTokenLoaded);

        var baseInterval = options.Value.PollIntervalSeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WatcherDbContext>();

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

                    allNew.AddRange(await db.GetNewAsync(matched, stoppingToken));
                }

                logger.LogInformation("polled, {Count} new", allNew.Count);
            }

            var jitteredSeconds = Random.Shared.Next(baseInterval - 30, baseInterval + 30);
            await pollSignal.WaitAsync(TimeSpan.FromSeconds(jitteredSeconds), stoppingToken);
        }
    }
}
