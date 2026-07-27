using MarketplaceWatcher.Api;
using MarketplaceWatcher.Config;
using MarketplaceWatcher.Data;
using MarketplaceWatcher.Notifications;
using MarketplaceWatcher.Scraping;
using MarketplaceWatcher.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Log messages already use named placeholders ("{Count} new" rather than string
// interpolation), which is what makes them *structured* — the values stay as discrete
// fields rather than being flattened into text. This just makes the console rendering
// of that readable: timestamps, one line per entry.
builder.Logging.AddSimpleConsole(o =>
{
    o.TimestampFormat = "HH:mm:ss ";
    o.SingleLine = true;
});
builder.Services.Configure<WatcherOptions>(builder.Configuration.GetSection(WatcherOptions.Section));
builder.Services.AddDbContext<WatcherDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Watcher")));
builder.Services.AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection(TelegramOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<INotifier, TelegramNotifier>((serviceProvider, client) =>
    {
        var telegram = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
        // Trailing slash matters: without it, the relative "sendMessage" would replace
        // the last path segment instead of being appended to it.
        client.BaseAddress = new Uri($"https://api.telegram.org/bot{telegram.BotToken}/");
        client.Timeout = TimeSpan.FromSeconds(15);
    })
    .AddStandardResilienceHandler();

builder.Services.AddSingleton<PollSignal>();
builder.Services.AddSingleton<WatcherStatus>();
builder.Services.AddSingleton<IMarketplaceScraper, MarketplaceScraper>();
builder.Services.AddHostedService<PollingWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<WatcherDbContext>().Database.Migrate();
}

app.MapWatcherEndpoints();
app.Run();
