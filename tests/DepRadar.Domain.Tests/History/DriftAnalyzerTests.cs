using DepRadar.Domain.History;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.History;

public sealed class DriftAnalyzerTests
{
    private static readonly DateTimeOffset Monday = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Tuesday = Monday.AddDays(1);

    [Fact]
    public void Flags_a_package_that_newly_became_vulnerable_deprecated_and_archived()
    {
        var baseline = Snapshot(Monday, 100, RiskLevel.None,
            State("core", "1.0.0", 100, RiskLevel.None));

        var latest = Snapshot(Tuesday, 40, RiskLevel.High,
            State("core", "1.0.0", 40, RiskLevel.High, deprecated: true, archived: true, advisories: ["GHSA-xxxx"]));

        var report = DriftAnalyzer.Compare(baseline, latest);

        report.NetHealthDelta.ShouldBe(-60);
        report.Events.ShouldContain(e => e.Kind == DriftEventKind.BecameVulnerable && e.Detail.Contains("GHSA-xxxx", StringComparison.Ordinal));
        report.Events.ShouldContain(e => e.Kind == DriftEventKind.BecameDeprecated);
        report.Events.ShouldContain(e => e.Kind == DriftEventKind.BecameArchived);
        report.Events.ShouldContain(e => e.Kind == DriftEventKind.HealthRegressed);
        // Worst-first ordering: a High event leads.
        report.Events[0].Severity.ShouldBe(RiskLevel.High);
    }

    [Fact]
    public void Detects_added_removed_packages_and_cleared_advisories()
    {
        var baseline = Snapshot(Monday, 70, RiskLevel.High,
            State("core", "1.0.0", 70, RiskLevel.High, advisories: ["GHSA-old"]),
            State("legacy", "1.0.0", 100, RiskLevel.None));

        var latest = Snapshot(Tuesday, 100, RiskLevel.None,
            State("core", "1.0.0", 100, RiskLevel.None),
            State("newdep", "2.0.0", 100, RiskLevel.None));

        var report = DriftAnalyzer.Compare(baseline, latest);

        report.NetHealthDelta.ShouldBe(30);
        report.Events.ShouldContain(e => e.Kind == DriftEventKind.AdvisoryCleared && e.Detail.Contains("GHSA-old", StringComparison.Ordinal));
        report.Events.ShouldContain(e => e.Kind == DriftEventKind.PackageAdded && e.Package == "newdep");
        report.Events.ShouldContain(e => e.Kind == DriftEventKind.PackageRemoved && e.Package == "legacy");
        report.Events.ShouldContain(e => e.Kind == DriftEventKind.HealthImproved && e.Package == "core");
    }

    [Fact]
    public void Reports_no_events_when_nothing_changed()
    {
        var states = new[] { State("core", "1.0.0", 100, RiskLevel.None) };
        var baseline = Snapshot(Monday, 100, RiskLevel.None, states);
        var latest = Snapshot(Tuesday, 100, RiskLevel.None, states);

        var report = DriftAnalyzer.Compare(baseline, latest);

        report.Events.ShouldBeEmpty();
        report.NetHealthDelta.ShouldBe(0);
    }

    private static ScanSnapshot Snapshot(DateTimeOffset at, int overallScore, RiskLevel overallLevel, params PackageRiskState[] states) =>
        ScanSnapshot.Create(PackageId.Create("core"), at, overallScore, overallLevel, states);

    private static PackageRiskState State(
        string id,
        string version,
        int score,
        RiskLevel level,
        bool deprecated = false,
        bool archived = false,
        bool stale = false,
        IReadOnlyList<string>? advisories = null) =>
        new(id, version, score, level, deprecated, archived, stale, advisories ?? [], "MIT");
}
