using DepRadar.Application.Messaging;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Risk;

/// <summary>
/// Handles <see cref="GetGraphRiskQuery"/>: rolls the per-node assessments up into a
/// worst-case project view (worst first).
/// </summary>
public sealed class GetGraphRiskHandler(GraphAssessmentLoader loader)
    : IRequestHandler<GetGraphRiskQuery, GraphRiskDto?>
{
    /// <inheritdoc />
    public async Task<GraphRiskDto?> Handle(GetGraphRiskQuery request, CancellationToken cancellationToken)
    {
        var rootId = PackageId.Create(request.PackageId);

        var assessment = await loader.LoadAsync(rootId, cancellationToken);
        if (assessment is null)
        {
            return null;
        }

        var scored = assessment.Nodes
            .OrderByDescending(node => node.Assessment.Score.Level)
            .ThenBy(node => node.Assessment.Score.Value)
            .ToList();

        var packages = scored
            .Select(node => PackageRiskDto.FromAssessment(node.Package, node.Version, node.Assessment))
            .ToList();

        var overallScore = scored.Count == 0 ? 100 : scored.Min(node => node.Assessment.Score.Value);
        var overallLevel = scored.Count == 0 ? RiskLevel.None : scored.Max(node => node.Assessment.Score.Level);

        return new GraphRiskDto(rootId.Original, overallScore, overallLevel.ToString(), packages.Count, packages);
    }
}
