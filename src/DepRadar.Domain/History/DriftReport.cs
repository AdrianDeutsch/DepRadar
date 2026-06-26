using DepRadar.Domain.ValueObjects;

namespace DepRadar.Domain.History;

/// <summary>
/// The drift of a package's dependency health between two scans: the events that
/// occurred and the net change in overall health.
/// </summary>
/// <param name="Root">The scanned root package.</param>
/// <param name="From">When the baseline snapshot was taken.</param>
/// <param name="To">When the latest snapshot was taken.</param>
/// <param name="NetHealthDelta">Latest overall score minus the baseline (negative = worse).</param>
/// <param name="Events">The detected changes, worst first.</param>
public sealed record DriftReport(
    PackageId Root,
    DateTimeOffset From,
    DateTimeOffset To,
    int NetHealthDelta,
    IReadOnlyList<DriftEvent> Events);
