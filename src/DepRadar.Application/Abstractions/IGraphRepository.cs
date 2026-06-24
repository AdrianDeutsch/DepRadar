using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Persistence port for the dependency graph: idempotent writes of a resolved graph
/// and a transitive-closure read backed by a recursive CTE.
/// </summary>
public interface IGraphRepository
{
    /// <summary>
    /// Idempotently writes the graph: discovered packages and versions are inserted
    /// if missing, edges are upserted by identity. Commit via <see cref="IUnitOfWork"/>.
    /// </summary>
    Task UpsertGraphAsync(
        IReadOnlyCollection<Package> packages,
        IReadOnlyCollection<PackageVersion> versions,
        IReadOnlyCollection<DependencyEdge> edges,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the transitive closure of edges reachable from <paramref name="root"/>,
    /// computed with a recursive CTE in PostgreSQL.
    /// </summary>
    Task<IReadOnlyList<GraphEdgeRow>> GetTransitiveClosureAsync(PackageId root, CancellationToken cancellationToken);
}

/// <summary>
/// A flat edge row as returned by the recursive-closure query. Values are raw
/// strings (already converted out of their value-object form by the SQL projection).
/// </summary>
public sealed record GraphEdgeRow(
    string DependentId,
    string DependentVersion,
    string DependencyId,
    string DependencyVersion,
    string VersionRange,
    bool IsDirect,
    int Depth);
