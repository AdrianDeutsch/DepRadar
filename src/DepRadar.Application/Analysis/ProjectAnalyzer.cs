using DepRadar.Application.Abstractions;
using DepRadar.Application.Risk;
using DepRadar.Domain.Packages;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Analysis;

/// <summary>
/// Resolves and scores a package's dependency graph in-process, with no database.
/// It produces the very same <see cref="GraphAssessment"/> as the persisted-scan
/// path, so every downstream feature (SBOM, report, chat, policy) works unchanged —
/// the only difference is the source of the data. This is what lets the CLI run
/// standalone (no server, no Postgres) for shift-left CI checks.
/// </summary>
public sealed class ProjectAnalyzer(
    IDependencyGraphResolver resolver,
    IVulnerabilitySource vulnerabilitySource,
    IPackageMetadataSource metadataSource,
    IRepositoryHealthSource repositoryHealthSource,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Advisory lookups per node are independent, so they run concurrently — bounded
    /// well below the resilience handler's rate limits (a 77-node graph would otherwise
    /// make 77 sequential round trips).
    /// </summary>
    private const int MaxConcurrentLookups = 8;

    /// <summary>
    /// Resolves <paramref name="root"/>'s transitive graph from NuGet, scores every
    /// node, and returns the assessed graph — or <see langword="null"/> if the package
    /// (or the pinned version) does not exist. <paramref name="pinnedVersion"/> selects
    /// an exact root version, used by upgrade-impact diffs.
    /// </summary>
    public async Task<GraphAssessment?> AnalyzeAsync(PackageId root, SemVer? pinnedVersion, CancellationToken cancellationToken)
    {
        var graph = await resolver.ResolveAsync(root, pinnedVersion, cancellationToken);
        if (graph is null)
        {
            return null;
        }

        // Repository health is fetched for the root only (one extra metadata + GitHub
        // call), mirroring the scan pipeline which enriches the scanned package.
        var (rootArchived, rootStale) = await ResolveRootHealthAsync(root, cancellationToken);

        using var gate = new SemaphoreSlim(MaxConcurrentLookups);
        var nodes = (await Task.WhenAll(graph.Nodes.Select(async node =>
        {
            await gate.WaitAsync(cancellationToken);
            IReadOnlyList<VulnerabilityRecord> advisories;
            try
            {
                advisories = await vulnerabilitySource.GetAsync(node.Id, node.Version, cancellationToken);
            }
            finally
            {
                gate.Release();
            }

            var vulnerabilities = advisories
                .Select(a => PackageVulnerability.Create(node.Id, node.Version, a.AdvisoryId, a.Severity, a.Summary, a.Source))
                .ToList();

            var input = new PackageRiskInput(
                node.Id,
                node.Version,
                ParseLicense(node.License),
                ParseLicense(node.LatestLicense),
                node.IsDeprecated,
                node.IsRoot && rootArchived,
                node.IsRoot && rootStale,
                vulnerabilities);

            return new AssessedNode(node.Id, node.Version, input, PackageRiskScorer.Assess(input));
        }))).ToList();

        var edges = graph.Edges
            .Select(e => new GraphEdgeRow(
                e.FromId.Value,
                e.FromVersion.ToString(),
                e.ToId.Value,
                e.ToVersion.ToString(),
                e.VersionRange,
                e.IsDirect,
                e.IsDirect ? 1 : 2))
            .ToList();

        return new GraphAssessment(root, nodes, edges, graph.Truncated);
    }

    /// <summary>Best-effort root repository health; any failure degrades to "no signal".</summary>
    private async Task<(bool Archived, bool Stale)> ResolveRootHealthAsync(PackageId root, CancellationToken cancellationToken)
    {
#pragma warning disable CA1031 // Best-effort enrichment: a missing/unreachable repo must not fail analysis.
        try
        {
            var metadata = await metadataSource.GetAsync(root, cancellationToken);
            if (metadata?.SourceRepositoryUrl is not { } repositoryUrl)
            {
                return (false, false);
            }

            var health = await repositoryHealthSource.GetAsync(repositoryUrl, cancellationToken);
            if (health is null)
            {
                return (false, false);
            }

            var stale = health.LastPushedAt is { } last
                && last < timeProvider.GetUtcNow() - MaintenanceThresholds.StaleAfter;
            return (health.Archived, stale);
        }
        catch (Exception)
        {
            return (false, false);
        }
#pragma warning restore CA1031
    }

    private static SpdxLicense? ParseLicense(string? identifier) =>
        string.IsNullOrWhiteSpace(identifier) ? null : SpdxLicense.Create(identifier);
}
