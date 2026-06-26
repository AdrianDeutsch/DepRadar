using System.Collections.Concurrent;
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

    // Roots currently in actionable drift, maintained as scans complete in this process.
    private static readonly ConcurrentDictionary<string, byte> PackagesInDrift = new(StringComparer.Ordinal);

    /// <summary>Counts scans that completed successfully.</summary>
    public static readonly Counter<long> ScansCompleted = Meter.CreateCounter<long>("depradar.scans.completed");

    /// <summary>Counts packages discovered across scans.</summary>
    public static readonly Counter<long> PackagesDiscovered = Meter.CreateCounter<long>("depradar.packages.discovered");

    static DepRadarTelemetry() =>
        Meter.CreateObservableGauge(
            "depradar.drift.open",
            () => (long)PackagesInDrift.Count,
            unit: "{package}",
            description: "Packages currently in actionable (high-severity) drift.");

    /// <summary>The current number of packages in actionable drift (also exposed as a gauge).</summary>
    public static long OpenDriftCount => PackagesInDrift.Count;

    /// <summary>Records that a package is now in actionable drift.</summary>
    public static void MarkDrift(string packageId) => PackagesInDrift[packageId] = 0;

    /// <summary>Records that a package's drift has cleared.</summary>
    public static void ClearDrift(string packageId) => PackagesInDrift.TryRemove(packageId, out _);
}
