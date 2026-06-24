using DepRadar.Api;
using DepRadar.Api.Endpoints;
using DepRadar.Application;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks, service discovery, resilience.
builder.AddServiceDefaults();

// DbContext with the pgvector mapping enabled. The "depradardb" connection string
// is supplied by the AppHost via Aspire's service reference.
builder.Services.AddDepRadarDbContext(builder.Configuration.GetConnectionString("depradardb"));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration["DepsDev:BaseUrl"],
    builder.Configuration["NuGet:BaseUrl"],
    builder.Configuration["Osv:BaseUrl"],
    builder.Configuration["Anthropic:ApiKey"],
    builder.Configuration["Anthropic:Model"]);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    // Interactive API reference at /scalar/v1.
    app.MapScalarApiReference();

    // Slice 1 bootstraps the schema with EnsureCreated; EF Core migrations are
    // introduced with the persistence hardening (Slice 6).
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DepRadarDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.MapPackageEndpoints();
app.MapScanEndpoints();

await app.RunAsync();
