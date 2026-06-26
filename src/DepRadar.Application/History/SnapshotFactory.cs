using DepRadar.Application.Risk;
using DepRadar.Domain.History;
using DepRadar.Domain.Risk;

namespace DepRadar.Application.History;

/// <summary>
/// Projects an assessed graph into a persistable <see cref="ScanSnapshot"/> — the
/// bridge between the live assessment and the drift history.
/// </summary>
public static class SnapshotFactory
{
    /// <summary>Builds a snapshot of <paramref name="assessment"/> taken at <paramref name="takenAt"/>.</summary>
    public static ScanSnapshot From(GraphAssessment assessment, DateTimeOffset takenAt)
    {
        var states = assessment.Nodes
            .Select(node => new PackageRiskState(
                node.Package.Value,
                node.Version.ToString(),
                node.Assessment.Score.Value,
                node.Assessment.Score.Level,
                node.Input.IsDeprecated,
                node.Input.IsArchived,
                node.Input.IsRepositoryStale,
                node.Input.Vulnerabilities.Select(v => v.AdvisoryId).Distinct(StringComparer.Ordinal).ToList(),
                node.Input.ResolvedLicense?.Identifier))
            .ToList();

        var overallScore = states.Count == 0 ? 100 : states.Min(s => s.Score);
        var overallLevel = states.Count == 0 ? RiskLevel.None : states.Max(s => s.Level);

        return ScanSnapshot.Create(assessment.Root, takenAt, overallScore, overallLevel, states);
    }
}
