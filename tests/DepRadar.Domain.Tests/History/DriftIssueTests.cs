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

        DriftIssue.Title(report).ShouldContain("root");
        DriftIssue.Title(report).ShouldContain("1 new high-severity");

        var body = DriftIssue.Body(report);
        body.ShouldContain("newtonsoft.json");
        body.ShouldContain("GHSA-x");
        body.ShouldNotContain("misc"); // Medium is not actionable
    }
}
