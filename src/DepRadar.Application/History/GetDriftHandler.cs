using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Domain.History;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.History;

/// <summary>
/// Handles <see cref="GetDriftQuery"/>: loads the two most recent snapshots and diffs
/// them over time. With a single scan there is no baseline yet.
/// </summary>
public sealed class GetDriftHandler(IScanSnapshotRepository snapshots)
    : IRequestHandler<GetDriftQuery, DriftReportDto?>
{
    /// <inheritdoc />
    public async Task<DriftReportDto?> Handle(GetDriftQuery request, CancellationToken cancellationToken)
    {
        var root = PackageId.Create(request.PackageId);
        var recent = await snapshots.GetRecentAsync(root, 2, cancellationToken);
        if (recent.Count == 0)
        {
            return null;
        }

        var latest = recent[0];
        if (recent.Count == 1)
        {
            // Baseline recorded, nothing to compare against yet.
            return new DriftReportDto(root.Value, HasBaseline: false, From: null, latest.CreatedAt, 0, []);
        }

        var report = DriftAnalyzer.Compare(recent[1], latest);
        var events = report.Events
            .Select(e => new DriftEventDto(e.Package, e.Kind.ToString(), e.Detail, e.Severity.ToString()))
            .ToList();

        return new DriftReportDto(root.Value, HasBaseline: true, report.From, report.To, report.NetHealthDelta, events);
    }
}
