using DepRadar.Application.Abstractions;
using DepRadar.Application.Analysis;
using DepRadar.Application.Ecosystems;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;
using DepRadar.Infrastructure.External.Npm;

namespace DepRadar.Infrastructure.External.Go;

/// <summary>
/// Wires the Go proxy resolver + vulnerability source into the same stateless
/// <see cref="ProjectAnalyzer"/> the NuGet path uses (with no-op metadata/repo-health,
/// since those signals are NuGet-specific), and exposes it as <see cref="IGoScanner"/>.
/// </summary>
internal sealed class GoScanner(
    GoProxyClient proxy,
    GoDependencyGraphResolver resolver,
    GoVulnerabilitySource vulnerabilities,
    IExploitIntelligenceSource exploitIntelligence,
    TimeProvider timeProvider) : IGoScanner
{
    /// <inheritdoc />
    public async Task<GraphAssessment?> ScanAsync(string module, string? version, CancellationToken cancellationToken)
    {
        var name = module.Trim();

        // Go requirements are exact — the only normalization is the optional v prefix.
        SemVer? pinned = null;
        if (!string.IsNullOrWhiteSpace(version) && !GoVersion.TryParse(version, out pinned!))
        {
            return null;
        }

        var advisories = new ExploitAwareVulnerabilitySource(vulnerabilities, exploitIntelligence);
        var analyzer = new ProjectAnalyzer(resolver, advisories, NullMetadataSource.Instance, NullRepositoryHealthSource.Instance, timeProvider);
        return await analyzer.AnalyzeAsync(PackageId.FromNormalized(name), pinned, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SemVer>> ListVersionsAsync(string module, CancellationToken cancellationToken)
    {
        var versions = new List<SemVer>();
        foreach (var raw in await proxy.ListVersionsAsync(module.Trim(), cancellationToken))
        {
            if (GoVersion.TryParse(raw, out var version))
            {
                versions.Add(version);
            }
        }

        return versions;
    }

    /// <inheritdoc />
    public async Task<GraphAssessment?> ScanLockedAsync(IReadOnlyList<LockedPackage> packages, CancellationToken cancellationToken)
    {
        var nodes = new List<ResolvedNode>();
        foreach (var locked in packages)
        {
            if (!GoVersion.TryParse(locked.Version, out var version))
            {
                continue;
            }

            var id = PackageId.FromNormalized(locked.Name.Trim());
            nodes.Add(await resolver.BuildNodeAsync(id, version, isRoot: nodes.Count == 0, cancellationToken));
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
