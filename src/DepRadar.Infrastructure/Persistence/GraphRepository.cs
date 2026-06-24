using DepRadar.Application.Abstractions;
using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DepRadar.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IGraphRepository"/>. Writes are idempotent
/// (insert-if-missing keyed by identity); the closure is a recursive CTE so the
/// transitive hull never materializes nested object graphs in the application.
/// </summary>
internal sealed class GraphRepository(DepRadarDbContext dbContext) : IGraphRepository
{
    private const int MaxClosureDepth = 32;

    /// <inheritdoc />
    public async Task UpsertGraphAsync(
        IReadOnlyCollection<Package> packages,
        IReadOnlyCollection<PackageVersion> versions,
        IReadOnlyCollection<DependencyEdge> edges,
        CancellationToken cancellationToken)
    {
        var packageIds = packages.Select(p => p.Id).Distinct().ToList();

        var existingPackages = (await dbContext.Packages
            .AsNoTracking()
            .Where(p => packageIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var package in packages.Where(package => existingPackages.Add(package.Id)))
        {
            await dbContext.Packages.AddAsync(package, cancellationToken);
        }

        var existingVersions = (await dbContext.PackageVersions
            .AsNoTracking()
            .Where(v => packageIds.Contains(v.PackageId))
            .Select(v => new { v.PackageId, v.Version })
            .ToListAsync(cancellationToken))
            .Select(v => (v.PackageId.Value, v.Version.ToString()))
            .ToHashSet();

        foreach (var version in versions)
        {
            if (existingVersions.Add((version.PackageId.Value, version.Version.ToString())))
            {
                await dbContext.PackageVersions.AddAsync(version, cancellationToken);
            }
        }

        var existingEdges = (await dbContext.DependencyEdges
            .AsNoTracking()
            .Where(e => packageIds.Contains(e.DependentId))
            .Select(e => new { e.DependentId, e.DependentVersion, e.DependencyId })
            .ToListAsync(cancellationToken))
            .Select(e => (e.DependentId.Value, e.DependentVersion.ToString(), e.DependencyId.Value))
            .ToHashSet();

        foreach (var edge in edges)
        {
            if (existingEdges.Add((edge.DependentId.Value, edge.DependentVersion.ToString(), edge.DependencyId.Value)))
            {
                await dbContext.DependencyEdges.AddAsync(edge, cancellationToken);
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GraphEdgeRow>> GetTransitiveClosureAsync(PackageId root, CancellationToken cancellationToken)
    {
        var rootValue = root.Value;

        // Recursive CTE: seed with the root's edges, then chain each edge's resolved
        // target onto the next edge's dependent. MIN(depth) collapses diamond paths.
        var rows = await dbContext.Database
            .SqlQuery<GraphEdgeRow>($"""
                WITH RECURSIVE closure AS (
                    SELECT e."DependentId", e."DependentVersion", e."DependencyId", e."DependencyVersion",
                           e."VersionRange", e."IsDirect", 1 AS "Depth"
                    FROM depradar."dependency_edges" e
                    WHERE e."DependentId" = {rootValue}
                    UNION
                    SELECT e."DependentId", e."DependentVersion", e."DependencyId", e."DependencyVersion",
                           e."VersionRange", e."IsDirect", c."Depth" + 1
                    FROM depradar."dependency_edges" e
                    INNER JOIN closure c
                        ON e."DependentId" = c."DependencyId" AND e."DependentVersion" = c."DependencyVersion"
                    WHERE c."Depth" < {MaxClosureDepth}
                )
                SELECT "DependentId", "DependentVersion", "DependencyId", "DependencyVersion",
                       "VersionRange", "IsDirect", MIN("Depth") AS "Depth"
                FROM closure
                GROUP BY "DependentId", "DependentVersion", "DependencyId", "DependencyVersion", "VersionRange", "IsDirect"
                ORDER BY MIN("Depth")
                """)
            .ToListAsync(cancellationToken);

        return rows;
    }
}
