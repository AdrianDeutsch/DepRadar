using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DepRadar.Application.Observability;

/// <summary>
/// DepRadar's custom OpenTelemetry instruments. The source/meter name is registered
/// with OpenTelemetry in <c>ServiceDefaults</c>, so scans show up as traces + metrics.
/// </summary>
public static class DepRadarTelemetry
{
    /// <summary>The OpenTelemetry source/meter name (registered in ServiceDefaults).</summary>
    public const string Name = "DepRadar";

    /// <summary>Activity source for scan spans.</summary>
    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    /// <summary>Counts scans that completed successfully.</summary>
    public static readonly Counter<long> ScansCompleted = Meter.CreateCounter<long>("depradar.scans.completed");

    /// <summary>Counts packages discovered across scans.</summary>
    public static readonly Counter<long> PackagesDiscovered = Meter.CreateCounter<long>("depradar.packages.discovered");
}
