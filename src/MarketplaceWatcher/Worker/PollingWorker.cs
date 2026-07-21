namespace MarketplaceWatcher.Worker;

public class PollingWorker(
    ILogger<PollingWorker> logger,
    IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Raw config read (Phase 2 replaces this with strongly-typed Options).
        var banner = config["Watcher:Banner"] ?? "(no Watcher:Banner configured)";
        logger.LogInformation("PollingWorker starting. Active banner = {Banner}", banner);

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "heartbeat — a real poll would run here at {Time:HH:mm:ss}",
                DateTimeOffset.Now);

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
