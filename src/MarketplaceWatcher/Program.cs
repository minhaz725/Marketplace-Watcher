using MarketplaceWatcher.Config;
using MarketplaceWatcher.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<WatcherOptions>(builder.Configuration.GetSection(WatcherOptions.Section));
builder.Services.AddHostedService<PollingWorker>();

var host = builder.Build();
host.Run();
