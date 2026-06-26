using DepRadar.Application;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using DepRadar.Worker.Pipeline;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDepRadarDbContext(builder.Configuration.GetConnectionString("depradardb"));

// Redis L2 for HybridCache when orchestrated by Aspire; absent it falls back to L1.
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("cache")))
{
    builder.AddRedisDistributedCache("cache");
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration["DepsDev:BaseUrl"],
    builder.Configuration["NuGet:BaseUrl"],
    builder.Configuration["Osv:BaseUrl"],
    builder.Configuration["Anthropic:ApiKey"],
    builder.Configuration["Anthropic:Model"],
    builder.Configuration["GitHub:Token"],
    builder.Configuration["Alerts:SlackWebhookUrl"],
    builder.Configuration["Alerts:GitHubRepo"]);

// Ingestion pipeline: one shared queue, a DB poller (producer) and a consumer.
builder.Services.AddSingleton<ScanDispatchQueue>();
builder.Services.AddHostedService<ScanPollingService>();
builder.Services.AddHostedService<ScanConsumerService>();

// Resilience: requeue scans abandoned by a crashed worker.
builder.Services.AddHostedService<StaleScanReaper>();

// Autonomous monitoring: periodically re-scan tracked packages (opt-in via Watch:IntervalHours).
builder.Services.AddHostedService<WatchlistRescanService>();

var host = builder.Build();
await host.RunAsync();
