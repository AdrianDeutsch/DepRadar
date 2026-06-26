using DepRadar.Application.Observability;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Observability;

public sealed class DepRadarTelemetryTests
{
    [Fact]
    public void Marking_and_clearing_drift_updates_the_open_drift_gauge()
    {
        var package = $"telemetry-{Guid.NewGuid():N}";
        var before = DepRadarTelemetry.OpenDriftCount;

        DepRadarTelemetry.MarkDrift(package);
        DepRadarTelemetry.OpenDriftCount.ShouldBe(before + 1);

        DepRadarTelemetry.MarkDrift(package); // idempotent — same package
        DepRadarTelemetry.OpenDriftCount.ShouldBe(before + 1);

        DepRadarTelemetry.ClearDrift(package);
        DepRadarTelemetry.OpenDriftCount.ShouldBe(before);
    }
}
