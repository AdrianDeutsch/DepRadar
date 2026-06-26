using DepRadar.Application.History;
using DepRadar.Domain.History;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.History;

public sealed class DriftIssueTests
{
    [Fact]
    public void Title_and_body_describe_only_the_actionable_events()
    {
        var report = new DriftReport(
            PackageId.Create("root"),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddDays(1),
            -30,
            [
                new DriftEvent("newtonsoft.json", DriftEventKind.BecameVulnerable, "new advisory GHSA-x", RiskLevel.High),
                new DriftEvent("misc", DriftEventKind.HealthRegressed, "health 90 → 80", RiskLevel.Medium),
            ]);

        // The title is stable (one issue per root) so repeat alerts can de-duplicate.
        DriftIssue.Title(report).ShouldBe("DepRadar: drift in root");

        var body = DriftIssue.Body(report);
        body.ShouldContain("1 new high-severity");
        body.ShouldContain("newtonsoft.json");
        body.ShouldContain("GHSA-x");
        body.ShouldNotContain("misc"); // Medium is not actionable
    }

    [Fact]
    public void Title_is_identical_for_the_same_root_regardless_of_the_events()
    {
        var first = new DriftReport(PackageId.Create("acme.lib"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, -10,
            [new DriftEvent("acme.lib", DriftEventKind.BecameDeprecated, "deprecated", RiskLevel.High)]);
        var second = new DriftReport(PackageId.Create("acme.lib"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, -40,
            [new DriftEvent("dep", DriftEventKind.BecameVulnerable, "new advisory", RiskLevel.High)]);

        DriftIssue.Title(first).ShouldBe(DriftIssue.Title(second));
    }
}
