using DepRadar.Application;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using DepRadar.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<DepRadarDbContext>("depradardb");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration["DepsDev:BaseUrl"]);

builder.Services.AddHostedService<IngestionWorker>();

var host = builder.Build();
await host.RunAsync();
