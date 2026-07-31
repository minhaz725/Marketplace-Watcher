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

// The Vite dev server runs on its own origin (:5173), and a browser blocks cross-origin
// fetches unless the API opts in. Named policy, dev origin only — not AllowAnyOrigin.
const string DevCorsPolicy = "vite-dev";
builder.Services.AddCors(options => options.AddPolicy(DevCorsPolicy, policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddSingleton<IMarketplaceScraper, MarketplaceScraper>();
builder.Services.AddHostedService<PollingWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<WatcherDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCorsPolicy);
}

// Serve the built React app from wwwroot when it exists, so the published binary is the
// whole product — no Vite process alongside it. Absent in dev (you run Vite instead), so
// this is a no-op there. publish.sh is what puts the build here.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapWatcherEndpoints();

// Anything not matched by an API route or a real file falls back to the SPA shell.
app.MapFallbackToFile("index.html");

app.Run();
