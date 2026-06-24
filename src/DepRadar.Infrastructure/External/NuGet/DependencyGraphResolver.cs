using DepRadar.Application.Abstractions;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace DepRadar.Infrastructure.External.NuGet;

/// <summary>
/// Resolves the transitive dependency graph by walking NuGet registration metadata
/// breadth-first, resolving each declared range to the concrete version NuGet would
/// install (lowest version that satisfies the range).
/// </summary>
/// <remarks>
/// Bounded by <see cref="MaxNodes"/> to keep a scan tractable and avoid hammering the
/// NuGet API; per-package metadata is cached for the duration of one resolution.
/// </remarks>
internal sealed class DependencyGraphResolver(NuGetClient nuGetClient, ILogger<DependencyGraphResolver> logger)
    : IDependencyGraphResolver
{
    private const int MaxNodes = 200;

    /// <inheritdoc />
    public async Task<ResolvedGraph?> ResolveAsync(PackageId root, CancellationToken cancellationToken)
    {
        var cache = new Dictionary<string, NuGetPackageData?>(StringComparer.Ordinal);

        var rootData = await GetDataAsync(root, cache, cancellationToken);
        if (rootData is null || rootData.Versions.Count == 0)
        {
            return null;
        }

        var rootNuGetVersion = SelectRootVersion(rootData);
        if (!SemVer.TryParse(rootNuGetVersion.ToNormalizedString(), out var rootVersion))
        {
            return null;
        }

        var nodes = new List<ResolvedNode>();
        var edges = new List<ResolvedEdge>();
        var visited = new HashSet<(string Id, string Version)>();
        var queue = new Queue<(PackageId Id, NuGetVersion Version, SemVer SemVer)>();
        var truncated = false;

        nodes.Add(BuildNode(root, rootNuGetVersion, rootVersion, rootData, isRoot: true));
        visited.Add((root.Value, rootNuGetVersion.ToNormalizedString()));
        queue.Enqueue((root, rootNuGetVersion, rootVersion));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var data = await GetDataAsync(current.Id, cache, cancellationToken);
            var versionData = data?.Versions.FirstOrDefault(v => v.Version.Equals(current.Version));
            if (versionData is null)
            {
                continue;
            }

            foreach (var dependency in versionData.Dependencies)
            {
                if (!TryCreatePackageId(dependency.Id, out var dependencyId))
                {
                    continue;
                }

                var dependencyData = await GetDataAsync(dependencyId, cache, cancellationToken);
                if (dependencyData is null || dependencyData.Versions.Count == 0)
                {
                    continue;
                }

                var range = ParseRange(dependency.Range);
                var resolved = range.FindBestMatch(dependencyData.Versions.Select(v => v.Version));
                if (resolved is null || !SemVer.TryParse(resolved.ToNormalizedString(), out var resolvedSemVer))
                {
                    continue;
                }

                edges.Add(new ResolvedEdge(
                    current.Id,
                    current.SemVer,
                    dependencyId,
                    resolvedSemVer,
                    range.ToNormalizedString(),
                    IsDirect: current.Id == root));

                var key = (dependencyId.Value, resolved.ToNormalizedString());
                if (!visited.Add(key))
                {
                    continue;
                }

                if (nodes.Count >= MaxNodes)
                {
                    truncated = true;
                    continue;
                }

                nodes.Add(BuildNode(dependencyId, resolved, resolvedSemVer, dependencyData, isRoot: false));
                queue.Enqueue((dependencyId, resolved, resolvedSemVer));
            }
        }

        logger.LogInformation(
            "Resolved {Root}@{Version}: {Nodes} node(s), {Edges} edge(s){Truncated}.",
            root.Original,
            rootVersion,
            nodes.Count,
            edges.Count,
            truncated ? " (truncated)" : string.Empty);

        return new ResolvedGraph(root, rootVersion, nodes, edges, truncated);
    }

    private async Task<NuGetPackageData?> GetDataAsync(
        PackageId id,
        Dictionary<string, NuGetPackageData?> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(id.Value, out var cached))
        {
            return cached;
        }

        var data = await nuGetClient.GetPackageDataAsync(id, cancellationToken);
        cache[id.Value] = data;
        return data;
    }

    /// <summary>Builds a graph node with the risk-relevant facts for its version.</summary>
    private static ResolvedNode BuildNode(PackageId id, NuGetVersion nuGetVersion, SemVer version, NuGetPackageData data, bool isRoot)
    {
        var versionData = data.Versions.FirstOrDefault(v => v.Version.Equals(nuGetVersion));
        var (latestStable, latestLicense) = ComputeLatestFacts(data);

        return new ResolvedNode(
            id,
            version,
            isRoot,
            License: versionData?.License,
            IsDeprecated: versionData?.IsDeprecated ?? false,
            LatestStableVersion: latestStable,
            LatestLicense: latestLicense);
    }

    /// <summary>The latest stable version and its license (for license-shift detection).</summary>
    private static (SemVer? Version, string? License) ComputeLatestFacts(NuGetPackageData data)
    {
        var stable = data.Versions.Where(v => !v.Version.IsPrerelease).ToList();
        IReadOnlyList<NuGetVersionData> pool = stable.Count > 0 ? stable : data.Versions;

        var latest = pool.MaxBy(v => v.Version);
        if (latest is null || !SemVer.TryParse(latest.Version.ToNormalizedString(), out var version))
        {
            return (null, null);
        }

        return (version, latest.License);
    }

    /// <summary>Highest stable listed version, falling back to the highest overall.</summary>
    private static NuGetVersion SelectRootVersion(NuGetPackageData data)
    {
        var stable = data.Versions.Where(v => !v.Version.IsPrerelease).Select(v => v.Version).ToList();
        var pool = stable.Count > 0 ? stable : data.Versions.Select(v => v.Version).ToList();
        return pool.Max()!;
    }

    private static VersionRange ParseRange(string? range) =>
        string.IsNullOrWhiteSpace(range) || !VersionRange.TryParse(range, out var parsed)
            ? VersionRange.All
            : parsed;

    private static bool TryCreatePackageId(string rawId, out PackageId id)
    {
        try
        {
            id = PackageId.Create(rawId);
            return true;
        }
        catch (ArgumentException)
        {
            id = default;
            return false;
        }
    }
}
