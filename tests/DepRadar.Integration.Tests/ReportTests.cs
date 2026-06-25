using DepRadar.Application;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Application.Reports;
using DepRadar.Application.Scans;
using DepRadar.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// End-to-end report generation against real PostgreSQL: scan a package, then build
/// the Markdown audit report from the stored graph, risk and upgrade data.
/// </summary>
public sealed class ReportTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Report_contains_ranking_and_recommendation_after_a_scan()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await EnsureSchemaAsync(provider);

        var queued = await SendAsync(provider, new RequestScanCommand("ReportRoot"));
        await SendAsync(provider, new RunScanCommand(queued.Id));

        var report = await SendAsync(provider, new GetPackageReportQuery("ReportRoot"));

        report.ShouldNotBeNull();
        report!.ShouldContain("# DepRadar report — ReportRoot");
        report.ShouldContain("## Risk ranking");
        report.ShouldContain("Critical"); // B's stubbed critical advisory
        report.ShouldContain("## Upgrade advice");
    }

    private static async Task EnsureSchemaAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DepRadar.Infrastructure.Persistence.DepRadarDbContext>();
        await context.Database.MigrateAsync();
    }

    private static async Task<TResponse> SendAsync<TResponse>(IServiceProvider provider, IRequest<TResponse> request)
    {
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request);
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDepRadarDbContext(connectionString);
        services.AddApplication();
        services.AddInfrastructure();
        services.AddScoped<IDependencyGraphResolver, StubGraphResolver>();
        services.AddScoped<IVulnerabilitySource, StubVulnerabilitySource>();

        return services.BuildServiceProvider();
    }
}
