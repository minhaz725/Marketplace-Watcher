using MarketplaceWatcher.Api;
using MarketplaceWatcher.Config;
using MarketplaceWatcher.Data;
using MarketplaceWatcher.Scraping;
using MarketplaceWatcher.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<WatcherOptions>(builder.Configuration.GetSection(WatcherOptions.Section));
builder.Services.AddDbContext<WatcherDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Watcher")));
builder.Services.AddHostedService<PollingWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<WatcherDbContext>().Database.Migrate();
}

// Phase 6 verification only — proves CDP attach works. Replaced by the real
// per-watch pipeline in Phase 8.
if (args.Contains("--test-cdp"))
{
    await using var cdp = await CdpConnection.ConnectAsync();
    var page = await cdp.Context.NewPageAsync();
    await page.GotoAsync("https://www.facebook.com/marketplace/");
    var text = await page.InnerTextAsync("body");
    Console.WriteLine($"Page text length: {text.Length}");
    Console.WriteLine(text[..Math.Min(500, text.Length)]);
    return;
}

app.MapWatcherEndpoints();
app.Run();
