using MarketplaceWatcher.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<PollingWorker>();

var host = builder.Build();
host.Run();
