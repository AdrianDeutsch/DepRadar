using DepRadar.Application.History;
using DepRadar.Domain.History;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.History;

public sealed class DriftAlertTests
{
    [Fact]
    public void Actionable_keeps_only_high_severity_events()
    {
        var report = Report(
            new DriftEvent("a", DriftEventKind.BecameVulnerable, "new advisory X", RiskLevel.High),
            new DriftEvent("b", DriftEventKind.HealthRegressed, "health 80 → 70", RiskLevel.Medium),
            new DriftEvent("c", DriftEventKind.HealthImproved, "health 70 → 90", RiskLevel.None));

        var actionable = DriftAlert.Actionable(report);

        actionable.Count.ShouldBe(1);
        actionable[0].Package.ShouldBe("a");
    }

    [Fact]
    public void Format_builds_a_slack_message_listing_the_actionable_events()
    {
        var report = Report(
            new DriftEvent("newtonsoft.json", DriftEventKind.BecameVulnerable, "new advisory GHSA-x", RiskLevel.High),
            new DriftEvent("misc", DriftEventKind.HealthRegressed, "health 90 → 80", RiskLevel.Medium));

        var message = DriftAlertMessage.Format(report);

        message.ShouldContain("DepRadar drift");
        message.ShouldContain("newtonsoft.json");
        message.ShouldContain("GHSA-x");
        message.ShouldNotContain("misc"); // Medium is not actionable
    }

    private static DriftReport Report(params DriftEvent[] events) =>
        new(PackageId.Create("root"), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), -10, events);
}
