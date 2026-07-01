using DepRadar.Application.Abstractions;
using DepRadar.Application.Ecosystems;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DepRadar.Infrastructure.External.Go;

/// <summary>
/// Resolves a Go module's dependency graph from the module proxy. Mirrors the other
/// ecosystems' resolvers — the same <see cref="ResolvedGraph"/> flows through the rest
/// of the pipeline unchanged.
/// </summary>
/// <remarks>
/// Go requirements are exact versions (minimal version selection), so there is no
/// range resolution: the BFS follows each module version's own direct (non-indirect)
/// <c>require</c> lines. This is the DECLARED graph — MVS would collapse multiple
/// required versions of one module to the maximum; keeping them all is the
/// conservative choice for risk scanning (ADR 0026). The proxy serves no license
/// metadata, so license findings are absent for Go.
/// </remarks>
internal sealed class GoDependencyGraphResolver(GoProxyClient proxy, ILogger<GoDependencyGraphResolver> logger)
    : IDependencyGraphResolver
{
    private const int MaxNodes = 200;

    /// <inheritdoc />
    public async Task<ResolvedGraph?> ResolveAsync(PackageId root, SemVer? pinnedVersion, CancellationToken cancellationToken)
    {
        var rootRaw = await SelectRootVersionAsync(root.Value, pinnedVersion, cancellationToken);
        if (rootRaw is null || !GoVersion.TryParse(rootRaw, out var rootVersion))
        {
            return null;
        }

        var nodes = new List<ResolvedNode>();
        var edges = new List<ResolvedEdge>();
        var visited = new HashSet<(string Id, string Version)>();
        var queue = new Queue<(PackageId Id, SemVer Version, string Raw)>();
        var truncated = false;

        nodes.Add(await BuildNodeAsync(root, rootVersion, isRoot: true, cancellationToken));
        visited.Add((root.Value, rootVersion.ToString()));
        queue.Enqueue((root, rootVersion, rootRaw));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var modFile = await proxy.GetModFileAsync(current.Id.Value, current.Raw, cancellationToken);
            if (modFile is null)
            {
                continue;
            }

            foreach (var requirement in GoMod.ParseRequires(modFile))
            {
                if (!GoVersion.TryParse(requirement.Specifier, out var required))
                {
                    continue;
                }

                var dependencyId = PackageId.FromNormalized(requirement.Name);
                edges.Add(new ResolvedEdge(current.Id, current.Version, dependencyId, required, requirement.Specifier, IsDirect: current.Id == root));

                if (!visited.Add((dependencyId.Value, required.ToString())))
                {
                    continue;
                }

                if (nodes.Count >= MaxNodes)
                {
                    truncated = true;
                    continue;
                }

                nodes.Add(await BuildNodeAsync(dependencyId, required, isRoot: false, cancellationToken));
                queue.Enqueue((dependencyId, required, requirement.Specifier));
            }
        }

        logger.LogInformation("Resolved Go module {Root}@{Version}: {Nodes} node(s), {Edges} edge(s).", root.Value, rootVersion, nodes.Count, edges.Count);
        return new ResolvedGraph(root, rootVersion, nodes, edges, truncated);
    }

    /// <summary>Builds a node's facts — the proxy has no license/deprecation data, only versions.</summary>
    internal async Task<ResolvedNode> BuildNodeAsync(PackageId id, SemVer version, bool isRoot, CancellationToken cancellationToken)
    {
        var latestStable = LatestStable(await proxy.ListVersionsAsync(id.Value, cancellationToken));
        return new ResolvedNode(
            id,
            version,
            isRoot,
            License: null,
            IsDeprecated: false,
            LatestStableVersion: latestStable,
            LatestLicense: null);
    }

    /// <summary>The highest stable tagged version, or null when the module has no usable tags.</summary>
    internal static SemVer? LatestStable(IReadOnlyList<string> rawVersions)
    {
        SemVer? best = null;
        foreach (var raw in rawVersions)
        {
            if (GoVersion.TryParse(raw, out var version) && version.IsStable && (best is null || version > best))
            {
                best = version;
            }
        }

        return best;
    }

    // Pinned: verify the version exists (tag list, else pseudo-versions resolve via the
    // .mod endpoint later). Latest: @latest, falling back to the highest stable tag.
    private async Task<string?> SelectRootVersionAsync(string module, SemVer? pinnedVersion, CancellationToken cancellationToken)
    {
        if (pinnedVersion is not null)
        {
            var raw = $"v{pinnedVersion}";
            return await proxy.GetModFileAsync(module, raw, cancellationToken) is null ? null : raw;
        }

        return await proxy.GetLatestVersionAsync(module, cancellationToken);
    }
}
