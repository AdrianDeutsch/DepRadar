using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Risk;

/// <summary>
/// Handles <see cref="GetGraphRiskQuery"/>: scores every package in the transitive
/// graph and rolls them up into a worst-case project view.
/// </summary>
public sealed class GetGraphRiskHandler(
    IPackageRepository packageRepository,
    IGraphRepository graphRepository,
    IRiskRepository riskRepository)
    : IRequestHandler<GetGraphRiskQuery, GraphRiskDto?>
{
    /// <inheritdoc />
    public async Task<GraphRiskDto?> Handle(GetGraphRiskQuery request, CancellationToken cancellationToken)
    {
        var rootId = PackageId.Create(request.PackageId);

        if (await packageRepository.GetAsync(rootId, cancellationToken) is null)
        {
            return null;
        }

        var targets = await CollectNodesAsync(rootId, cancellationToken);
        var inputs = await riskRepository.GetRiskInputsAsync(targets, cancellationToken);

        var scored = inputs
            .Select(input => (input, assessment: PackageRiskScorer.Assess(input)))
            .OrderByDescending(x => x.assessment.Score.Level)
            .ThenBy(x => x.assessment.Score.Value)
            .ToList();

        var packages = scored
            .Select(x => PackageRiskDto.FromAssessment(x.input.Package, x.input.Version, x.assessment))
            .ToList();

        var overallScore = scored.Count == 0 ? 100 : scored.Min(x => x.assessment.Score.Value);
        var overallLevel = scored.Count == 0 ? RiskLevel.None : scored.Max(x => x.assessment.Score.Level);

        return new GraphRiskDto(rootId.Original, overallScore, overallLevel.ToString(), packages.Count, packages);
    }

    /// <summary>Distinct (package, version) nodes of the closure; the root if it has no edges.</summary>
    private async Task<IReadOnlyCollection<(PackageId Package, SemVer Version)>> CollectNodesAsync(PackageId rootId, CancellationToken cancellationToken)
    {
        var rows = await graphRepository.GetTransitiveClosureAsync(rootId, cancellationToken);

        var nodes = new HashSet<(string Id, string Version)>();
        foreach (var row in rows)
        {
            nodes.Add((row.DependentId, row.DependentVersion));
            nodes.Add((row.DependencyId, row.DependencyVersion));
        }

        var targets = nodes
            .Where(node => SemVer.TryParse(node.Version, out _))
            .Select(node => (PackageId.FromNormalized(node.Id), SemVer.Parse(node.Version)))
            .ToList();

        if (targets.Count == 0)
        {
            // No dependencies stored: assess the root at its latest stored version.
            var versions = await packageRepository.GetVersionsAsync(rootId, cancellationToken);
            if (versions.Count > 0)
            {
                targets.Add((rootId, versions.Max(v => v.Version)!));
            }
        }

        return targets;
    }
}
