using DepRadar.Application;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Badges;
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
        await AddSnapshotAsync(provider, "drifty", monday, 100, RiskLevel.None,
            new PackageRiskState("drifty", "1.0.0", 100, RiskLevel.None, false, false, false, [], "MIT"));
        await AddSnapshotAsync(provider, "drifty", monday.AddDays(1), 40, RiskLevel.High,
            new PackageRiskState("drifty", "1.0.0", 40, RiskLevel.High, true, false, false, ["GHSA-drift"], "MIT"));

        var drift = await SendAsync(provider, new GetDriftQuery("drifty"));

        drift.ShouldNotBeNull();
        drift!.HasBaseline.ShouldBeTrue();
        drift.NetHealthDelta.ShouldBe(-60);
        drift.Events.ShouldContain(e => e.Kind == nameof(DriftEventKind.BecameVulnerable));
        drift.Events.ShouldContain(e => e.Kind == nameof(DriftEventKind.BecameDeprecated));
    }

    [Fact]
    public async Task A_rescan_into_new_risk_fires_an_alert_and_the_badge_reflects_health()
    {
        var notifier = new CapturingDriftNotifier();
        await using var provider = BuildProvider(fixture.ConnectionString, notifier);
        await MigrateAsync(provider);

        // Baseline: the same graph as the stub, but healthy (no advisories, not deprecated).
        var baselineAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await AddSnapshotAsync(provider, "AlertRoot", baselineAt, 100, RiskLevel.None,
            new PackageRiskState("alertroot", "1.0.0", 100, RiskLevel.None, false, false, false, [], "MIT"),
            new PackageRiskState("b", "2.0.0", 100, RiskLevel.None, false, false, false, [], "MIT"),
            new PackageRiskState("c", "3.0.0", 100, RiskLevel.None, false, false, false, [], "Apache-2.0"));

        // A real scan records the stub's risky state (B vulnerable, C deprecated).
        var queued = await SendAsync(provider, new RequestScanCommand("AlertRoot"));
        await SendAsync(provider, new RunScanCommand(queued.Id));

        notifier.Reports.Count.ShouldBe(1);
        var actionable = DriftAlert.Actionable(notifier.Reports[0]);
        actionable.ShouldContain(e => e.Package == "b" && e.Kind == DriftEventKind.BecameVulnerable);
        actionable.ShouldContain(e => e.Package == "c" && e.Kind == DriftEventKind.BecameDeprecated);

        // The health badge reflects the scanned package.
        var badge = await SendAsync(provider, new GetBadgeQuery("AlertRoot"));
        badge.ShouldStartWith("<svg");
        badge.ShouldContain("DepRadar");
    }

    [Fact]
    public async Task A_rescan_with_no_new_risk_resolves_the_alert()
    {
        var notifier = new CapturingDriftNotifier();
        await using var provider = BuildProvider(fixture.ConnectionString, notifier);
        await MigrateAsync(provider);

        // Baseline ALREADY carries the stub's risky state, so the re-scan introduces no
        // *new* high-severity drift — the alert should be resolved, not re-fired.
        var baselineAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await AddSnapshotAsync(provider, "ResolveRoot", baselineAt, 35, RiskLevel.Critical,
            new PackageRiskState("resolveroot", "1.0.0", 85, RiskLevel.Medium, false, false, false, [], "MIT"),
            new PackageRiskState("b", "2.0.0", 50, RiskLevel.Critical, false, false, false, ["GHSA-test-0001"], "GPL-3.0-only"),
            new PackageRiskState("c", "3.0.0", 70, RiskLevel.High, true, false, false, [], "Apache-2.0"));

        var queued = await SendAsync(provider, new RequestScanCommand("ResolveRoot"));
        await SendAsync(provider, new RunScanCommand(queued.Id));

        notifier.Reports.ShouldBeEmpty();             // nothing new to alert on
        notifier.Resolved.ShouldContain("resolveroot"); // a prior alert would be closed
    }

    [Fact]
    public async Task Retention_keeps_only_the_newest_snapshots_per_root()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await MigrateAsync(provider);

        var start = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        for (var day = 0; day < 4; day++)
        {
            await AddSnapshotAsync(provider, "Retained", start.AddDays(day), 100, RiskLevel.None,
                new PackageRiskState("retained", "1.0.0", 100, RiskLevel.None, false, false, false, [], "MIT"));
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IScanSnapshotRepository>();
            var removed = await repository.PruneAsync(PackageId.Create("Retained"), keep: 2, CancellationToken.None);
            removed.ShouldBe(2);

            var remaining = await repository.GetRecentAsync(PackageId.Create("Retained"), 10, CancellationToken.None);
            remaining.Count.ShouldBe(2);
            remaining[0].CreatedAt.ShouldBe(start.AddDays(3)); // newest kept
        }
    }

    [Fact]
    public async Task Drift_status_badge_reflects_no_baseline_then_open_issues()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await MigrateAsync(provider);

        // No history yet → "no baseline".
        (await SendAsync(provider, new GetDriftBadgeQuery("BadgeFresh"))).ShouldContain("no baseline");

        // A healthy baseline then a risky latest → actionable drift → "issue(s)".
        var august = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        await AddSnapshotAsync(provider, "BadgeDrift", august, 100, RiskLevel.None,
            new PackageRiskState("badgedrift", "1.0.0", 100, RiskLevel.None, false, false, false, [], "MIT"));
        await AddSnapshotAsync(provider, "BadgeDrift", august.AddDays(1), 40, RiskLevel.High,
            new PackageRiskState("badgedrift", "1.0.0", 40, RiskLevel.High, true, false, false, ["GHSA-badge"], "MIT"));

        var badge = await SendAsync(provider, new GetDriftBadgeQuery("BadgeDrift"));
        badge.ShouldStartWith("<svg");
        badge.ShouldContain("issue");
    }

    [Fact]
    public async Task Digest_summarizes_drift_across_tracked_roots()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await MigrateAsync(provider);

        var july = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        await AddSnapshotAsync(provider, "DigestRoot", july, 100, RiskLevel.None,
            new PackageRiskState("digestroot", "1.0.0", 100, RiskLevel.None, false, false, false, [], "MIT"));
        await AddSnapshotAsync(provider, "DigestRoot", july.AddDays(1), 40, RiskLevel.High,
            new PackageRiskState("digestroot", "1.0.0", 40, RiskLevel.High, true, false, false, ["GHSA-digest"], "MIT"));

        var digest = await SendAsync(provider, new GetDriftDigestQuery());

        digest.ShouldContain("# DepRadar drift digest");
        digest.ShouldContain("digestroot");
        digest.ShouldContain(nameof(DriftEventKind.BecameDeprecated));
    }

    private static async Task AddSnapshotAsync(
        IServiceProvider provider,
        string root,
        DateTimeOffset takenAt,
        int overallScore,
        RiskLevel overallLevel,
        params PackageRiskState[] states)
    {
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IScanSnapshotRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await repository.AddAsync(ScanSnapshot.Create(PackageId.Create(root), takenAt, overallScore, overallLevel, states), CancellationToken.None);
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

    private static ServiceProvider BuildProvider(string connectionString, IDriftNotifier? notifier = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDepRadarDbContext(connectionString);
        services.AddApplication();
        services.AddInfrastructure();
        services.AddScoped<IDependencyGraphResolver, StubGraphResolver>();
        services.AddScoped<IVulnerabilitySource, StubVulnerabilitySource>();
        services.AddScoped<IRepositoryHealthEnricher, StubRepositoryHealthEnricher>();
        if (notifier is not null)
        {
            services.AddSingleton<IDriftNotifier>(notifier);
        }

        return services.BuildServiceProvider();
    }
}
