namespace DepRadar.Application.Graphs;

/// <summary>API-facing read model for a package's transitive dependency graph.</summary>
public sealed record PackageGraphDto(
    string Root,
    bool Truncated,
    IReadOnlyList<GraphNodeDto> Nodes,
    IReadOnlyList<GraphEdgeDto> Edges);

/// <summary>A node (a resolved package version) in the graph.</summary>
public sealed record GraphNodeDto(string PackageId, string Version, bool IsRoot);

/// <summary>A directed, resolved edge in the graph.</summary>
public sealed record GraphEdgeDto(
    string FromId,
    string FromVersion,
    string ToId,
    string ToVersion,
    string VersionRange,
    bool IsDirect,
    int Depth);
