using DepRadar.Application;
using DepRadar.Application.Abstractions;
using DepRadar.Application.History;
using DepRadar.Application.Messaging;
using DepRadar.Application.Scans;
using DepRadar.Domain.History;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Drift history against real Postgres: a completed scan records a snapshot, and two
/// snapshots diff into drift events (exercising the jsonb round-trip end to end).
/// </summary>
public sealed class DriftTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task A_completed_scan_records_a_snapshot_so_the_first_drift_has_no_baseline_yet()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await MigrateAsync(provider);

        var queued = await SendAsync(provider, new RequestScanCommand("DriftBaseline"));
        await SendAsync(provider, new RunScanCommand(queued.Id));

        var drift = await SendAsync(provider, new GetDriftQuery("DriftBaseline"));

        drift.ShouldNotBeNull(); // a snapshot was recorded by the scan
        drift!.HasBaseline.ShouldBeFalse(); // only one snapshot exists so far
    }

    [Fact]
    public async Task Two_snapshots_diff_into_drift_events_over_real_postgres()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await MigrateAsync(provider);

        var monday = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await AddSnapshotAsync(provider, monday, 100, RiskLevel.None,
            new PackageRiskState("drifty", "1.0.0", 100, RiskLevel.None, false, false, false, [], "MIT"));
        await AddSnapshotAsync(provider, monday.AddDays(1), 40, RiskLevel.High,
            new PackageRiskState("drifty", "1.0.0", 40, RiskLevel.High, true, false, false, ["GHSA-drift"], "MIT"));

        var drift = await SendAsync(provider, new GetDriftQuery("drifty"));

        drift.ShouldNotBeNull();
        drift!.HasBaseline.ShouldBeTrue();
        drift.NetHealthDelta.ShouldBe(-60);
        drift.Events.ShouldContain(e => e.Kind == nameof(DriftEventKind.BecameVulnerable));
        drift.Events.ShouldContain(e => e.Kind == nameof(DriftEventKind.BecameDeprecated));
    }

    private static async Task AddSnapshotAsync(
        IServiceProvider provider,
        DateTimeOffset takenAt,
        int overallScore,
        RiskLevel overallLevel,
        params PackageRiskState[] states)
    {
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IScanSnapshotRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await repository.AddAsync(ScanSnapshot.Create(PackageId.Create("drifty"), takenAt, overallScore, overallLevel, states), CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
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
