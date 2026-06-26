using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Domain.History;

namespace DepRadar.Application.History;

/// <summary>
/// Handles <see cref="GetDriftDigestQuery"/>: computes drift for every tracked root and
/// renders the combined Markdown digest.
/// </summary>
public sealed class GetDriftDigestHandler(IScanSnapshotRepository snapshots, TimeProvider timeProvider)
    : IRequestHandler<GetDriftDigestQuery, string>
{
    /// <inheritdoc />
    public async Task<string> Handle(GetDriftDigestQuery request, CancellationToken cancellationToken)
    {
        var roots = await snapshots.GetTrackedRootsAsync(cancellationToken);

        var reports = new List<DriftReport>();
        foreach (var root in roots)
        {
            var recent = await snapshots.GetRecentAsync(root, 2, cancellationToken);
            if (recent.Count == 2)
            {
                reports.Add(DriftAnalyzer.Compare(recent[1], recent[0]));
            }
        }

        return DriftDigestBuilder.Render(reports, timeProvider.GetUtcNow());
    }
}
