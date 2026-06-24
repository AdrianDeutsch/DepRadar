using DepRadar.Application;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Application.Risk;
using DepRadar.Application.Scans;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// End-to-end risk scoring against real PostgreSQL: a scan stores license/deprecation
/// and advisories, then the risk queries surface the findings and roll them up.
/// </summary>
public sealed class RiskTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Scan_then_risk_surfaces_shift_copyleft_deprecation_and_vulnerabilities()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await EnsureSchemaAsync(provider);

        var queued = await SendAsync(provider, new RequestScanCommand("RiskRoot"));
        var completed = await SendAsync(provider, new RunScanCommand(queued.Id));
        completed.Status.ShouldBe("Completed");

        // Per-package: the root's latest version moved MIT -> commercial (the "MediatR case").
        var rootRisk = await SendAsync(provider, new GetPackageRiskQuery("RiskRoot"));
        rootRisk.ShouldNotBeNull();
        rootRisk!.Findings.ShouldContain(f => f.Code == "LICENSE_SHIFT" && f.Level == "High");

        // Project-level: B carries a critical advisory, so the whole graph is Critical.
        var graphRisk = await SendAsync(provider, new GetGraphRiskQuery("RiskRoot"));
        graphRisk.ShouldNotBeNull();
        graphRisk!.PackagesAssessed.ShouldBe(3);
        graphRisk.OverallLevel.ShouldBe("Critical");
        graphRisk.Packages[0].PackageId.ShouldBe("b"); // worst first

        var b = graphRisk.Packages.Single(p => p.PackageId == "b");
        b.Findings.ShouldContain(f => f.Category == "Security" && f.Level == "Critical");
        b.Findings.ShouldContain(f => f.Code == "COPYLEFT");

        var c = graphRisk.Packages.Single(p => p.PackageId == "c");
        c.Findings.ShouldContain(f => f.Code == "DEPRECATED");
    }

    private static async Task EnsureSchemaAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DepRadarDbContext>();
        await context.Database.EnsureCreatedAsync();
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
        services.AddDbContext<DepRadarDbContext>(options => options.UseNpgsql(connectionString));
        services.AddApplication();
        services.AddInfrastructure();
        services.AddScoped<IDependencyGraphResolver, StubGraphResolver>();
        services.AddScoped<IVulnerabilitySource, StubVulnerabilitySource>();

        return services.BuildServiceProvider();
    }
}
