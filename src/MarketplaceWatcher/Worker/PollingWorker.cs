using MarketplaceWatcher.Config;
using MarketplaceWatcher.Data;
using MarketplaceWatcher.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MarketplaceWatcher.Worker;

public class PollingWorker(
    ILogger<PollingWorker> logger,
    IOptions<WatcherOptions> options,
    IConfiguration config,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var watches = options.Value.Watches;
        logger.LogInformation(
            "PollingWorker starting. {Count} watch(es) configured: {Names}",
            watches.Count,
            string.Join(", ", watches.Select(w => w.Name ?? "(free-only / match-all)")));

        var botTokenLoaded = !string.IsNullOrEmpty(config["Telegram:BotToken"]);
        logger.LogInformation("Telegram bot token loaded from config: {Loaded}", botTokenLoaded);

        var baseInterval = options.Value.PollIntervalSeconds;

        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WatcherDbContext>();
                var newListings = await db.GetNewAsync(Enumerable.Empty<Listing>(), stoppingToken);
                logger.LogInformation("polled, {Count} new", newListings.Count);
            }

            var jitteredSeconds = Random.Shared.Next(baseInterval - 30, baseInterval + 30);
            await Task.Delay(TimeSpan.FromSeconds(jitteredSeconds), stoppingToken);
        }
    }
}
