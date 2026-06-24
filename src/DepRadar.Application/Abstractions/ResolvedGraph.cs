using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// A fully resolved dependency graph for one root package: every node carries a
/// concrete version and every edge a concrete target version (ranges already
/// resolved to the version NuGet would install).
/// </summary>
/// <param name="Root">The root package.</param>
/// <param name="RootVersion">The resolved version of the root package.</param>
/// <param name="Nodes">All distinct package versions in the graph (root included).</param>
/// <param name="Edges">The directed, resolved dependency edges.</param>
/// <param name="Truncated">True when traversal stopped early because the node cap was reached.</param>
public sealed record ResolvedGraph(
    PackageId Root,
    SemVer RootVersion,
    IReadOnlyList<ResolvedNode> Nodes,
    IReadOnlyList<ResolvedEdge> Edges,
    bool Truncated);

/// <summary>A package version that participates in the graph.</summary>
public sealed record ResolvedNode(PackageId Id, SemVer Version, bool IsRoot);

/// <summary>
/// A resolved dependency: <paramref name="FromId"/>@<paramref name="FromVersion"/>
/// depends on <paramref name="ToId"/>@<paramref name="ToVersion"/>, originally
/// declared with <paramref name="VersionRange"/>.
/// </summary>
public sealed record ResolvedEdge(
    PackageId FromId,
    SemVer FromVersion,
    PackageId ToId,
    SemVer ToVersion,
    string VersionRange,
    bool IsDirect);
