using DepRadar.Application.Abstractions;
using DepRadar.Application.Analysis;
using DepRadar.Application.Ecosystems;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;
using DepRadar.Infrastructure.External.Npm;

namespace DepRadar.Infrastructure.External.Cargo;

/// <summary>
/// Wires the crates.io resolver + vulnerability source into the same stateless
/// <see cref="ProjectAnalyzer"/> the NuGet path uses (with no-op metadata/repo-health,
/// since those signals are NuGet-specific), and exposes it as <see cref="ICargoScanner"/>.
/// </summary>
internal sealed class CargoScanner(
    CargoRegistryClient registry,
    CargoDependencyGraphResolver resolver,
    CargoVulnerabilitySource vulnerabilities,
    IExploitIntelligenceSource exploitIntelligence,
    TimeProvider timeProvider) : ICargoScanner
{
    /// <inheritdoc />
    public async Task<GraphAssessment?> ScanAsync(string crate, string? version, CancellationToken cancellationToken)
    {
        var name = crate.Trim().ToLowerInvariant();

        // A fully-specified version pins exactly (deterministic assessment of what was
        // declared); partial or operator forms are Cargo requirements (bare = caret!)
        // resolved against the published versions — an unsatisfiable spec is a miss,
        // not a silent fallback to latest.
        SemVer? pinned = null;
        if (!string.IsNullOrWhiteSpace(version))
        {
            if (!SemVer.TryParse(version, out pinned!))
            {
                var document = await registry.GetAsync(name, cancellationToken);
                pinned = CargoReq.BestMatch(version, CargoDependencyGraphResolver.SelectableVersions(document));
                if (pinned is null)
                {
                    return null;
                }
            }
        }

        var advisories = new ExploitAwareVulnerabilitySource(vulnerabilities, exploitIntelligence);
        var analyzer = new ProjectAnalyzer(resolver, advisories, NullMetadataSource.Instance, NullRepositoryHealthSource.Instance, timeProvider);
        return await analyzer.AnalyzeAsync(PackageId.FromNormalized(name), pinned, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SemVer>> ListVersionsAsync(string crate, CancellationToken cancellationToken)
    {
        var document = await registry.GetAsync(crate.Trim().ToLowerInvariant(), cancellationToken);
        return CargoDependencyGraphResolver.SelectableVersions(document);
    }

    /// <inheritdoc />
    public async Task<GraphAssessment?> ScanLockedAsync(IReadOnlyList<LockedPackage> packages, CancellationToken cancellationToken)
    {
        var nodes = new List<ResolvedNode>();
        foreach (var locked in packages)
        {
            if (!SemVer.TryParse(locked.Version, out var version))
            {
                continue;
            }

            var name = locked.Name.Trim().ToLowerInvariant();
            var id = PackageId.FromNormalized(name);

            // The registry document only feeds license/yanked/latest facts; a crate
            // that has vanished from the registry is still assessed on its advisories.
            var document = await registry.GetAsync(name, cancellationToken);
            nodes.Add(document is null
                ? new ResolvedNode(id, version, IsRoot: nodes.Count == 0, License: null, IsDeprecated: false, LatestStableVersion: null, LatestLicense: null)
                : CargoDependencyGraphResolver.BuildNode(id, version, document, isRoot: nodes.Count == 0));
        }

        if (nodes.Count == 0)
        {
            return null;
        }

        var graph = new ResolvedGraph(nodes[0].Id, nodes[0].Version, nodes, Edges: [], Truncated: false);
        var advisories = new ExploitAwareVulnerabilitySource(vulnerabilities, exploitIntelligence);
        var analyzer = new ProjectAnalyzer(new FixedGraphResolver(graph), advisories, NullMetadataSource.Instance, NullRepositoryHealthSource.Instance, timeProvider);
        return await analyzer.AnalyzeAsync(graph.Root, pinnedVersion: null, cancellationToken);
    }
}
