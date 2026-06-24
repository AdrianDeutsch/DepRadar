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

/// <summary>
/// A package version that participates in the graph, with the risk-relevant facts
/// captured at resolution time (cheap — they come from the same NuGet metadata).
/// </summary>
/// <param name="Id">The package id.</param>
/// <param name="Version">The resolved version.</param>
/// <param name="IsRoot">Whether this is the root of the scan.</param>
/// <param name="License">SPDX license of the resolved version, if declared.</param>
/// <param name="IsDeprecated">Whether the resolved version is deprecated on NuGet.</param>
/// <param name="LatestStableVersion">The package's latest stable version, if any.</param>
/// <param name="LatestLicense">SPDX license of the latest stable version, if declared.</param>
public sealed record ResolvedNode(
    PackageId Id,
    SemVer Version,
    bool IsRoot,
    string? License,
    bool IsDeprecated,
    SemVer? LatestStableVersion,
    string? LatestLicense);

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
