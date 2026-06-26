using DepRadar.Application.Messaging;

namespace DepRadar.Application.History;

/// <summary>
/// Query: how a package's dependency health has drifted since the previous scan.
/// Returns <see langword="null"/> if the package has never been scanned.
/// </summary>
/// <param name="PackageId">The root package id.</param>
public sealed record GetDriftQuery(string PackageId) : IRequest<DriftReportDto?>;

/// <summary>The drift between the two most recent scans, shaped for the API/dashboard.</summary>
/// <param name="Package">The root package id.</param>
/// <param name="HasBaseline">False when only one scan exists yet (nothing to compare to).</param>
/// <param name="From">Baseline snapshot time (null without a baseline).</param>
/// <param name="To">Latest snapshot time.</param>
/// <param name="NetHealthDelta">Latest overall score minus the baseline.</param>
/// <param name="Events">The detected changes, worst first.</param>
public sealed record DriftReportDto(
    string Package,
    bool HasBaseline,
    DateTimeOffset? From,
    DateTimeOffset To,
    int NetHealthDelta,
    IReadOnlyList<DriftEventDto> Events);

/// <summary>A single drift change.</summary>
/// <param name="Package">The affected package.</param>
/// <param name="Kind">What changed (e.g. <c>BecameVulnerable</c>).</param>
/// <param name="Detail">A human-readable explanation.</param>
/// <param name="Severity">How serious it is.</param>
public sealed record DriftEventDto(string Package, string Kind, string Detail, string Severity);
