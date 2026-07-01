using DepRadar.Application.Abstractions;
using DepRadar.Application.Ecosystems;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DepRadar.Infrastructure.External.Cargo;

/// <summary>
/// Resolves a crate's transitive dependency graph from crates.io, resolving each
/// declared requirement with <see cref="CargoReq"/>. Mirrors the NuGet/npm/PyPI
/// resolvers — the same <see cref="ResolvedGraph"/> flows through the rest of the
/// pipeline unchanged.
/// </summary>
/// <remarks>
/// Only <c>normal</c>, non-optional dependencies are followed (what <c>cargo build</c>
/// pulls for a downstream consumer; dev/build dependencies are not part of a dependent's
/// tree). A version's <c>yanked</c> flag maps onto the deprecation signal, and yanked
/// versions are excluded from requirement resolution — exactly Cargo's behavior.
/// </remarks>
internal sealed class CargoDependencyGraphResolver(CargoRegistryClient registry, ILogger<CargoDependencyGraphResolver> logger)
    : IDependencyGraphResolver
{
    private const int MaxNodes = 200;

    /// <inheritdoc />
    public async Task<ResolvedGraph?> ResolveAsync(PackageId root, SemVer? pinnedVersion, CancellationToken cancellationToken)
    {
        var cache = new Dictionary<string, CargoCrateDocument?>(StringComparer.Ordinal);

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
            var dependencies = await registry.GetDependenciesAsync(current.Id.Value, current.Version.ToString(), cancellationToken);

            foreach (var dependency in dependencies?.Dependencies ?? [])
            {
                // Dev/build dependencies are not part of a dependent's build; optional
                // ones only activate behind features — both skipped, matching cargo.
                if (dependency.CrateId is null || dependency.Req is null
                    || dependency.Optional || dependency.Kind is not (null or "normal"))
                {
                    continue;
                }

                var dependencyId = PackageId.FromNormalized(dependency.CrateId.ToLowerInvariant());
                var dependencyDocument = await GetAsync(dependencyId.Value, cache, cancellationToken);
                var available = SelectableVersions(dependencyDocument);
                var resolved = available.Count == 0 ? null : CargoReq.BestMatch(dependency.Req, available);
                if (resolved is null)
                {
                    continue;
                }

                edges.Add(new ResolvedEdge(current.Id, current.Version, dependencyId, resolved, dependency.Req, IsDirect: current.Id == root));

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

        logger.LogInformation("Resolved crate {Root}@{Version}: {Nodes} node(s), {Edges} edge(s).", root.Value, rootVersion, nodes.Count, edges.Count);
        return new ResolvedGraph(root, rootVersion, nodes, edges, truncated);
    }

    /// <summary>Builds a node's risk facts from its crate document — shared with the lockfile scan.</summary>
    internal static ResolvedNode BuildNode(PackageId id, SemVer version, CargoCrateDocument document, bool isRoot)
    {
        var selectable = SelectableVersions(document);
        var stable = selectable.Where(candidate => candidate.IsStable).ToList();
        var latestStable = (stable.Count > 0 ? stable : selectable).Count > 0
            ? (stable.Count > 0 ? stable : selectable).Max()
            : null;

        var entry = Entry(document, version);
        return new ResolvedNode(
            id,
            version,
            isRoot,
            License: entry?.License,
            // A yanked version is crates.io's "do not use this release" — the closest
            // analogue to a deprecation flag.
            IsDeprecated: entry?.Yanked ?? false,
            LatestStableVersion: latestStable,
            LatestLicense: latestStable is null ? entry?.License : Entry(document, latestStable)?.License);
    }

    /// <summary>All parseable, non-yanked versions — shared with the scanner's requirement resolution.</summary>
    internal static List<SemVer> SelectableVersions(CargoCrateDocument? document) =>
        document?.Versions is null
            ? []
            : document.Versions
                .Where(version => !version.Yanked && SemVer.TryParse(version.Num, out _))
                .Select(version => SemVer.Parse(version.Num!))
                .ToList();

    private static CargoVersion? Entry(CargoCrateDocument document, SemVer version) =>
        document.Versions?.FirstOrDefault(entry =>
            SemVer.TryParse(entry.Num, out var parsed) && parsed.Equals(version));

    private static SemVer? SelectRootVersion(CargoCrateDocument document, SemVer? pinnedVersion)
    {
        if (pinnedVersion is not null)
        {
            // A pinned scan may target a yanked version deliberately (that is the point
            // of scanning a lockfile), so match against every parseable version.
            return document.Versions?
                .Where(entry => SemVer.TryParse(entry.Num, out _))
                .Select(entry => SemVer.Parse(entry.Num!))
                .FirstOrDefault(candidate => candidate.Equals(pinnedVersion));
        }

        var selectable = SelectableVersions(document);
        if (selectable.Count == 0)
        {
            return null;
        }

        var stable = selectable.Where(version => version.IsStable).ToList();
        return (stable.Count > 0 ? stable : selectable).Max();
    }

    private async Task<CargoCrateDocument?> GetAsync(string crate, Dictionary<string, CargoCrateDocument?> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(crate, out var cached))
        {
            return cached;
        }

        var document = await registry.GetAsync(crate, cancellationToken);
        cache[crate] = document;
        return document;
    }
}
