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
builder.Services.AddSingleton<PollSignal>();
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

// Phase 7 reconnaissance only — dumps a few real listing cards' HTML so we
// can see the actual DOM shape (decoy text, class names) before writing
// TextReconstructor. Not part of the shipped app.
if (args.Contains("--dump-cards"))
{
    var urlArg = args.FirstOrDefault(a => a.StartsWith("--url="));
    var url = urlArg is not null
        ? urlArg["--url=".Length..]
        : "https://www.facebook.com/marketplace/category/free";

    await using var cdp = await CdpConnection.ConnectAsync();
    var page = await cdp.Context.NewPageAsync();
    await page.GotoAsync(url);
    await page.WaitForTimeoutAsync(3000); // let the feed render before we query it

    var cardsHtml = await page.EvalOnSelectorAllAsync<string[]>(
        "a[href*='/marketplace/item/']",
        "els => els.slice(0, 2).map(e => e.outerHTML)");

    for (var i = 0; i < cardsHtml.Length; i++)
    {
        Console.WriteLine($"--- card {i} ---");
        Console.WriteLine(cardsHtml[i]);
    }

    return;
}

app.MapWatcherEndpoints();
app.Run();
