using DepRadar.Application;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Application.Remediation;
using DepRadar.Application.Scans;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Remediation against real Postgres: scan the stub graph (B is vulnerable, fixed in
/// 3.0.0) and confirm the minimal safe upgrade.
/// </summary>
public sealed class RemediationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Suggests_the_minimal_safe_upgrade_for_each_vulnerable_package()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await MigrateAsync(provider);

        var queued = await SendAsync(provider, new RequestScanCommand("RemRoot"));
        await SendAsync(provider, new RunScanCommand(queued.Id));

        var result = await SendAsync(provider, new GetRemediationsQuery("RemRoot"));

        result.ShouldNotBeNull();
        var fix = result!.Remediations.ShouldHaveSingleItem(); // only B is vulnerable
        fix.Package.ShouldBe("b");
        fix.CurrentVersion.ShouldBe("2.0.0");
        fix.SafeVersion.ShouldBe("3.0.0");
        fix.Advisories.ShouldContain("GHSA-test-0001");
    }

    private static async Task MigrateAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DepRadarDbContext>();
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
        services.AddScoped<IRepositoryHealthEnricher, StubRepositoryHealthEnricher>();

        return services.BuildServiceProvider();
    }
}
