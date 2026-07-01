using DepRadar.Application.Abstractions;
using DepRadar.Application.Analysis;
using DepRadar.Application.Ecosystems;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;
using DepRadar.Infrastructure.External.Npm;

namespace DepRadar.Infrastructure.External.PyPi;

/// <summary>
/// Wires the PyPI resolver + PyPI vulnerability source into the same stateless
/// <see cref="ProjectAnalyzer"/> the NuGet path uses (with no-op metadata/repo-health,
/// since those signals are NuGet-specific), and exposes it as <see cref="IPyPiScanner"/>.
/// </summary>
internal sealed class PyPiScanner(
    PyPiRegistryClient registry,
    PyPiDependencyGraphResolver resolver,
    PyPiVulnerabilitySource vulnerabilities,
    IExploitIntelligenceSource exploitIntelligence,
    TimeProvider timeProvider) : IPyPiScanner
{
    /// <inheritdoc />
    public async Task<GraphAssessment?> ScanAsync(string package, string? version, CancellationToken cancellationToken)
    {
        var name = PyPiName.Normalize(package);

        // An exact final release pins directly; anything else is treated as a PEP 440
        // specifier set (>=, ~=, ==X.*, …) and resolved against the published releases —
        // an unsatisfiable spec is a miss, not a silent fallback to latest.
        SemVer? pinned = null;
        if (!string.IsNullOrWhiteSpace(version))
        {
            if (!PyPiVersion.TryParse(version, out pinned!))
            {
                var document = await registry.GetAsync(name, null, cancellationToken);
                pinned = PyPiSpecifier.BestMatch(version, PyPiDependencyGraphResolver.Versions(document));
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
    public async Task<IReadOnlyList<SemVer>> ListVersionsAsync(string package, CancellationToken cancellationToken)
    {
        var document = await registry.GetAsync(PyPiName.Normalize(package), null, cancellationToken);
        return PyPiDependencyGraphResolver.Versions(document);
    }

    /// <inheritdoc />
    public async Task<GraphAssessment?> ScanLockedAsync(IReadOnlyList<LockedPackage> packages, CancellationToken cancellationToken)
    {
        var nodes = new List<ResolvedNode>();
        foreach (var locked in packages)
        {
            if (!PyPiVersion.TryParse(locked.Version, out var version))
            {
                continue;
            }

            var name = PyPiName.Normalize(locked.Name);
            var id = PackageId.FromNormalized(name);

            // The registry document only feeds license/latest facts; a package that has
            // vanished from the registry is still assessed on its advisories.
            var document = await registry.GetAsync(name, null, cancellationToken);
            nodes.Add(document is null
                ? new ResolvedNode(id, version, IsRoot: nodes.Count == 0, License: null, IsDeprecated: false, LatestStableVersion: null, LatestLicense: null)
                : PyPiDependencyGraphResolver.BuildNode(id, version, document, isRoot: nodes.Count == 0));
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
