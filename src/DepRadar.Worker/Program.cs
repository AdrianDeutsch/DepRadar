using DepRadar.Application;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using DepRadar.Worker.Pipeline;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDepRadarDbContext(builder.Configuration.GetConnectionString("depradardb"));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration["DepsDev:BaseUrl"],
    builder.Configuration["NuGet:BaseUrl"],
    builder.Configuration["Osv:BaseUrl"],
    builder.Configuration["Anthropic:ApiKey"],
    builder.Configuration["Anthropic:Model"]);

// Ingestion pipeline: one shared queue, a DB poller (producer) and a consumer.
builder.Services.AddSingleton<ScanDispatchQueue>();
builder.Services.AddHostedService<ScanPollingService>();
builder.Services.AddHostedService<ScanConsumerService>();

var host = builder.Build();
await host.RunAsync();
