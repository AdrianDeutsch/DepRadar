using DepRadar.Application.Abstractions;
using DepRadar.Application.Analysis;
using DepRadar.Application.Ecosystems;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Infrastructure.External.Npm;

/// <summary>
/// Wires the npm resolver + npm vulnerability source into the same stateless
/// <see cref="ProjectAnalyzer"/> the NuGet path uses (with no-op metadata/repo-health,
/// since those signals are NuGet-specific), and exposes it as <see cref="INpmScanner"/>.
/// </summary>
internal sealed class NpmScanner(
    NpmRegistryClient registry,
    NpmDependencyGraphResolver resolver,
    NpmVulnerabilitySource vulnerabilities,
    IExploitIntelligenceSource exploitIntelligence,
    TimeProvider timeProvider) : INpmScanner
{
    /// <inheritdoc />
    public async Task<GraphAssessment?> ScanAsync(string package, string? version, CancellationToken cancellationToken)
    {
        var name = package.Trim().ToLowerInvariant();

        // An exact version pins directly; anything else is treated as an npm range
        // (^, ~, x-ranges, …) and resolved against the published versions — an
        // unsatisfiable spec is a miss, not a silent fallback to latest.
        SemVer? pinned = null;
        if (!string.IsNullOrWhiteSpace(version))
        {
            if (!SemVer.TryParse(version, out pinned!))
            {
                var document = await registry.GetAsync(name, cancellationToken);
                pinned = NpmRange.BestMatch(version, NpmDependencyGraphResolver.Versions(document));
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
        var document = await registry.GetAsync(package.Trim().ToLowerInvariant(), cancellationToken);
        return NpmDependencyGraphResolver.Versions(document);
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

            // The registry document only feeds license/latest facts; a package that has
            // vanished from the registry is still assessed on its advisories.
            var document = await registry.GetAsync(name, cancellationToken);
            nodes.Add(document is null
                ? new ResolvedNode(id, version, IsRoot: nodes.Count == 0, License: null, IsDeprecated: false, LatestStableVersion: null, LatestLicense: null)
                : NpmDependencyGraphResolver.BuildNode(id, version, document, isRoot: nodes.Count == 0));
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

/// <summary>No-op metadata source — npm has no deps.dev/NuGet catalog lookup.</summary>
internal sealed class NullMetadataSource : IPackageMetadataSource
{
    public static readonly NullMetadataSource Instance = new();

    public Task<PackageMetadata?> GetAsync(PackageId id, CancellationToken cancellationToken) =>
        Task.FromResult<PackageMetadata?>(null);
}

/// <summary>No-op repository-health source — skips root repo health for npm.</summary>
internal sealed class NullRepositoryHealthSource : IRepositoryHealthSource
{
    public static readonly NullRepositoryHealthSource Instance = new();

    public Task<RepositoryHealth?> GetAsync(Uri repositoryUrl, CancellationToken cancellationToken) =>
        Task.FromResult<RepositoryHealth?>(null);
}
