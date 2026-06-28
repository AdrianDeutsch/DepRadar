using System.Text.Json;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Ecosystems;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DepRadar.Infrastructure.External.Npm;

/// <summary>
/// Resolves an npm package's transitive dependency graph from the registry, resolving
/// each declared range with <see cref="NpmRange"/>. Mirrors the NuGet resolver, but for
/// the npm ecosystem — the same <see cref="ResolvedGraph"/> flows through the rest of the
/// pipeline unchanged.
/// </summary>
internal sealed class NpmDependencyGraphResolver(NpmRegistryClient registry, ILogger<NpmDependencyGraphResolver> logger)
    : IDependencyGraphResolver
{
    private const int MaxNodes = 200;

    /// <inheritdoc />
    public async Task<ResolvedGraph?> ResolveAsync(PackageId root, SemVer? pinnedVersion, CancellationToken cancellationToken)
    {
        var cache = new Dictionary<string, NpmPackageDocument?>(StringComparer.Ordinal);

        var rootDocument = await GetAsync(root.Value, cache, cancellationToken);
        if (rootDocument?.Versions is null || rootDocument.Versions.Count == 0)
        {
            return null;
        }

        var rootVersion = SelectRootVersion(rootDocument, pinnedVersion);
        if (rootVersion is null)
        {
            return null;
        }

        var nodes = new List<ResolvedNode>();
        var edges = new List<ResolvedEdge>();
        var visited = new HashSet<(string Id, string Version)>();
        var queue = new Queue<(PackageId Id, SemVer Version)>();
        var truncated = false;

        nodes.Add(BuildNode(root, rootVersion, rootDocument, isRoot: true));
        visited.Add((root.Value, rootVersion.ToString()));
        queue.Enqueue((root, rootVersion));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var document = await GetAsync(current.Id.Value, cache, cancellationToken);
            var versionDocument = Version(document, current.Version);
            if (versionDocument?.Dependencies is null)
            {
                continue;
            }

            foreach (var (dependencyName, range) in versionDocument.Dependencies)
            {
                var dependencyId = PackageId.FromNormalized(dependencyName);
                var dependencyDocument = await GetAsync(dependencyName, cache, cancellationToken);
                var available = Versions(dependencyDocument);
                var resolved = available.Count == 0 ? null : NpmRange.BestMatch(range, available);
                if (resolved is null)
                {
                    continue;
                }

                edges.Add(new ResolvedEdge(current.Id, current.Version, dependencyId, resolved, range, IsDirect: current.Id == root));

                if (!visited.Add((dependencyId.Value, resolved.ToString())))
                {
                    continue;
                }

                if (nodes.Count >= MaxNodes)
                {
                    truncated = true;
                    continue;
                }

                nodes.Add(BuildNode(dependencyId, resolved, dependencyDocument!, isRoot: false));
                queue.Enqueue((dependencyId, resolved));
            }
        }

        logger.LogInformation("Resolved npm {Root}@{Version}: {Nodes} node(s), {Edges} edge(s).", root.Value, rootVersion, nodes.Count, edges.Count);
        return new ResolvedGraph(root, rootVersion, nodes, edges, truncated);
    }

    private async Task<NpmPackageDocument?> GetAsync(string name, Dictionary<string, NpmPackageDocument?> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        var document = await registry.GetAsync(name, cancellationToken);
        cache[name] = document;
        return document;
    }

    private static ResolvedNode BuildNode(PackageId id, SemVer version, NpmPackageDocument document, bool isRoot)
    {
        var versionDocument = Version(document, version);
        var (latest, latestLicense) = LatestFacts(document);

        return new ResolvedNode(
            id,
            version,
            isRoot,
            License: License(versionDocument?.License),
            IsDeprecated: IsDeprecated(versionDocument?.Deprecated),
            LatestStableVersion: latest,
            LatestLicense: latestLicense);
    }

    private static (SemVer? Version, string? License) LatestFacts(NpmPackageDocument document)
    {
        var versions = Versions(document);
        if (versions.Count == 0)
        {
            return (null, null);
        }

        var stable = versions.Where(version => version.IsStable).ToList();
        var latest = (stable.Count > 0 ? stable : versions).Max();
        return (latest, License(Version(document, latest!)?.License));
    }

    private static SemVer? SelectRootVersion(NpmPackageDocument document, SemVer? pinnedVersion)
    {
        var versions = Versions(document);
        if (pinnedVersion is not null)
        {
            return versions.FirstOrDefault(version => version.Equals(pinnedVersion));
        }

        if (document.DistTags?.Latest is { } latest && SemVer.TryParse(latest, out var latestVersion))
        {
            return latestVersion;
        }

        var stable = versions.Where(version => version.IsStable).ToList();
        return (stable.Count > 0 ? stable : versions).Count > 0 ? (stable.Count > 0 ? stable : versions).Max() : null;
    }

    private static List<SemVer> Versions(NpmPackageDocument? document) =>
        document?.Versions is null
            ? []
            : document.Versions.Keys.Where(key => SemVer.TryParse(key, out _)).Select(SemVer.Parse).ToList();

    private static NpmVersionDocument? Version(NpmPackageDocument? document, SemVer version) =>
        document?.Versions is not null && document.Versions.TryGetValue(version.ToString(), out var versionDocument)
            ? versionDocument
            : null;

    private static string? License(JsonElement? license) =>
        license is { ValueKind: JsonValueKind.String } element ? element.GetString() : null;

    private static bool IsDeprecated(JsonElement? deprecated) =>
        deprecated is { ValueKind: JsonValueKind.String } element && !string.IsNullOrWhiteSpace(element.GetString());
}
