using DepRadar.Application.Abstractions;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Risk;

/// <summary>
/// Loads a package's transitive closure together with the scored assessment of every
/// node, once. Shared by the graph-risk, SBOM and chat features so the closure → risk
/// pipeline lives in one place.
/// </summary>
public sealed class GraphAssessmentLoader(
    IPackageRepository packageRepository,
    IGraphRepository graphRepository,
    IRiskRepository riskRepository)
{
    /// <summary>Loads the assessed graph, or <see langword="null"/> if the package was never scanned.</summary>
    public async Task<GraphAssessment?> LoadAsync(PackageId root, CancellationToken cancellationToken)
    {
        if (await packageRepository.GetAsync(root, cancellationToken) is null)
        {
            return null;
        }

        var edges = await graphRepository.GetTransitiveClosureAsync(root, cancellationToken);

        var nodeKeys = new HashSet<(string Id, string Version)>();
        foreach (var edge in edges)
        {
            nodeKeys.Add((edge.DependentId, edge.DependentVersion));
            nodeKeys.Add((edge.DependencyId, edge.DependencyVersion));
        }

        var targets = nodeKeys
            .Where(key => SemVer.TryParse(key.Version, out _))
            .Select(key => (PackageId.FromNormalized(key.Id), SemVer.Parse(key.Version)))
            .ToList();

        if (targets.Count == 0)
        {
            // No edges stored: assess the root at its latest stored version.
            var versions = await packageRepository.GetVersionsAsync(root, cancellationToken);
            if (versions.Count > 0)
            {
                targets.Add((root, versions.Max(v => v.Version)!));
            }
        }

        var inputs = await riskRepository.GetRiskInputsAsync(targets, cancellationToken);
        var nodes = inputs
            .Select(input => new AssessedNode(input.Package, input.Version, input, PackageRiskScorer.Assess(input)))
            .ToList();

        return new GraphAssessment(root, nodes, edges);
    }
}

/// <summary>A package version in the graph with its scoring input and assessment.</summary>
public sealed record AssessedNode(PackageId Package, SemVer Version, PackageRiskInput Input, RiskAssessment Assessment);

/// <summary>The assessed transitive graph: nodes (scored) + the raw edge rows.</summary>
public sealed record GraphAssessment(PackageId Root, IReadOnlyList<AssessedNode> Nodes, IReadOnlyList<GraphEdgeRow> Edges);
