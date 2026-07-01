using DepRadar.Application.Abstractions;
using DepRadar.Application.Ecosystems;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DepRadar.Infrastructure.External.PyPi;

/// <summary>
/// Resolves a PyPI package's transitive dependency graph from the JSON API, resolving
/// each <c>requires_dist</c> entry with <see cref="PyPiRequirement"/> + <see cref="PyPiSpecifier"/>.
/// Mirrors the NuGet/npm resolvers — the same <see cref="ResolvedGraph"/> flows through the
/// rest of the pipeline unchanged.
/// </summary>
/// <remarks>
/// PyPI splits a package across two documents: <c>/pypi/{name}/json</c> (latest info +
/// every release) and <c>/pypi/{name}/{version}/json</c> (that version's <c>requires_dist</c>).
/// The release list and license come from the former; a node's dependencies come from the
/// latter. Optional (extra-gated) requirements are skipped, matching a default <c>pip install</c>.
/// </remarks>
internal sealed class PyPiDependencyGraphResolver(PyPiRegistryClient registry, ILogger<PyPiDependencyGraphResolver> logger)
    : IDependencyGraphResolver
{
    private const int MaxNodes = 200;

    /// <inheritdoc />
    public async Task<ResolvedGraph?> ResolveAsync(PackageId root, SemVer? pinnedVersion, CancellationToken cancellationToken)
    {
        var cache = new DocumentCache(registry);

        var rootDocument = await cache.LatestAsync(root.Value, cancellationToken);
        var rootVersions = Versions(rootDocument);
        if (rootVersions.Count == 0)
        {
            return null;
        }

        var rootVersion = SelectRootVersion(rootDocument, rootVersions, pinnedVersion);
        if (rootVersion is null)
        {
            return null;
        }

        var nodes = new List<ResolvedNode>();
        var edges = new List<ResolvedEdge>();
        var visited = new HashSet<(string Id, string Version)>();
        var queue = new Queue<(PackageId Id, SemVer Version)>();
        var truncated = false;

        nodes.Add(BuildNode(root, rootVersion, rootDocument!, isRoot: true));
        visited.Add((root.Value, rootVersion.ToString()));
        queue.Enqueue((root, rootVersion));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            // requires_dist is per-version, so the node's own dependencies need the
            // versioned document rather than the package-level "latest" one.
            var versioned = await cache.VersionedAsync(current.Id.Value, current.Version.ToString(), cancellationToken);
            foreach (var requirement in Requirements(versioned))
            {
                if (requirement.Optional)
                {
                    continue;
                }

                var dependencyName = PyPiName.Normalize(requirement.Name);
                var dependencyId = PackageId.FromNormalized(dependencyName);
                var dependencyDocument = await cache.LatestAsync(dependencyName, cancellationToken);
                var available = Versions(dependencyDocument);
                var resolved = available.Count == 0 ? null : PyPiSpecifier.BestMatch(requirement.Specifier, available);
                if (resolved is null)
                {
                    continue;
                }

                edges.Add(new ResolvedEdge(current.Id, current.Version, dependencyId, resolved, requirement.Specifier, IsDirect: current.Id == root));

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

        logger.LogInformation("Resolved PyPI {Root}@{Version}: {Nodes} node(s), {Edges} edge(s).", root.Value, rootVersion, nodes.Count, edges.Count);
        return new ResolvedGraph(root, rootVersion, nodes, edges, truncated);
    }

    private static ResolvedNode BuildNode(PackageId id, SemVer version, PyPiDocument latest, bool isRoot)
    {
        var versions = Versions(latest);
        var stable = versions.Where(candidate => candidate.IsStable).ToList();
        var latestStable = (stable.Count > 0 ? stable : versions).Count > 0
            ? (stable.Count > 0 ? stable : versions).Max()
            : null;
        var license = NormalizeLicense(latest.Info?.License);

        return new ResolvedNode(
            id,
            version,
            isRoot,
            License: license,
            // PyPI has no package-level "deprecated" flag (yanking is per-file); the
            // security signal comes from OSV. Maintenance is approximated by license + age.
            IsDeprecated: false,
            LatestStableVersion: latestStable,
            LatestLicense: license);
    }

    private static SemVer? SelectRootVersion(PyPiDocument? document, List<SemVer> versions, SemVer? pinnedVersion)
    {
        if (pinnedVersion is not null)
        {
            return versions.FirstOrDefault(version => version.Equals(pinnedVersion));
        }

        if (PyPiVersion.TryParse(document?.Info?.Version, out var declaredLatest))
        {
            return declaredLatest;
        }

        var stable = versions.Where(version => version.IsStable).ToList();
        return (stable.Count > 0 ? stable : versions).Max();
    }

    /// <summary>All parseable final releases — shared with the scanner's specifier resolution.</summary>
    internal static List<SemVer> Versions(PyPiDocument? document)
    {
        if (document?.Releases is null)
        {
            return [];
        }

        var versions = new List<SemVer>();
        foreach (var key in document.Releases.Keys)
        {
            if (PyPiVersion.TryParse(key, out var version))
            {
                versions.Add(version);
            }
        }

        return versions;
    }

    private static IEnumerable<PyPiDependency> Requirements(PyPiDocument? document)
    {
        if (document?.Info?.RequiresDist is null)
        {
            yield break;
        }

        foreach (var entry in document.Info.RequiresDist)
        {
            if (PyPiRequirement.TryParse(entry, out var requirement))
            {
                yield return requirement;
            }
        }
    }

    // PyPI's "license" field is a free-text classifier that is often the full license body;
    // keep only short, identifier-like values (e.g. "MIT", "Apache-2.0") and drop prose.
    private static string? NormalizeLicense(string? license)
    {
        if (string.IsNullOrWhiteSpace(license))
        {
            return null;
        }

        var trimmed = license.Trim();
        return trimmed.Length is > 0 and <= 40 && !trimmed.Contains('\n') ? trimmed : null;
    }

    /// <summary>Per-resolution memo of fetched documents, keyed by "latest" vs an exact version.</summary>
    private sealed class DocumentCache(PyPiRegistryClient registry)
    {
        private readonly Dictionary<string, PyPiDocument?> _documents = new(StringComparer.Ordinal);

        public Task<PyPiDocument?> LatestAsync(string name, CancellationToken cancellationToken) =>
            GetAsync($"latest:{name}", name, null, cancellationToken);

        public Task<PyPiDocument?> VersionedAsync(string name, string version, CancellationToken cancellationToken) =>
            GetAsync($"v:{name}:{version}", name, version, cancellationToken);

        private async Task<PyPiDocument?> GetAsync(string key, string name, string? version, CancellationToken cancellationToken)
        {
            if (_documents.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var document = await registry.GetAsync(name, version, cancellationToken);
            _documents[key] = document;
            return document;
        }
    }
}
