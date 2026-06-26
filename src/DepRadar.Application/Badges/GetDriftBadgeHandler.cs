using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Domain.History;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Badges;

/// <summary>
/// Handles <see cref="GetDriftBadgeQuery"/>: renders a drift-status badge from the two
/// most recent snapshots (the same comparison the alerts use).
/// </summary>
public sealed class GetDriftBadgeHandler(IScanSnapshotRepository snapshots) : IRequestHandler<GetDriftBadgeQuery, string>
{
    /// <inheritdoc />
    public async Task<string> Handle(GetDriftBadgeQuery request, CancellationToken cancellationToken)
    {
        var root = PackageId.Create(request.PackageId);
        var recent = await snapshots.GetRecentAsync(root, 2, cancellationToken);
        if (recent.Count < 2)
        {
            return BadgeRenderer.RenderDrift(actionableCount: 0, hasBaseline: false);
        }

        var drift = DriftAnalyzer.Compare(recent[1], recent[0]);
        return BadgeRenderer.RenderDrift(DriftAlert.Actionable(drift).Count, hasBaseline: true);
    }
}
