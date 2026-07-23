using MarketplaceWatcher.Config;
using Microsoft.Extensions.Options;

namespace MarketplaceWatcher.Worker;

public class PollingWorker(
    ILogger<PollingWorker> logger,
    IOptions<WatcherOptions> options,
    IConfiguration config) : BackgroundService
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

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "heartbeat — a real poll would run here at {Time:HH:mm:ss}",
                DateTimeOffset.Now);

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
