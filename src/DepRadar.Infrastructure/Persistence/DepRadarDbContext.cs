using DepRadar.Application.Abstractions;
using DepRadar.Domain.Packages;
using Microsoft.EntityFrameworkCore;

namespace DepRadar.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work for the dependency graph. The graph is stored as flat
/// tables (packages, versions, edges) so the transitive closure can be computed
/// with recursive CTEs rather than nested object graphs.
/// </summary>
/// <remarks>
/// Implements <see cref="IUnitOfWork"/>: <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>
/// already satisfies the contract, so the application layer commits without ever
/// seeing an EF type.
/// </remarks>
public sealed class DepRadarDbContext(DbContextOptions<DepRadarDbContext> options)
    : DbContext(options), IUnitOfWork
{
    /// <summary>The package aggregates.</summary>
    public DbSet<Package> Packages => Set<Package>();

    /// <summary>The published versions of all packages.</summary>
    public DbSet<PackageVersion> PackageVersions => Set<PackageVersion>();

    /// <summary>The directed dependency-graph edges.</summary>
    public DbSet<DependencyEdge> DependencyEdges => Set<DependencyEdge>();

    /// <summary>The scan runs.</summary>
    public DbSet<Scan> Scans => Set<Scan>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("depradar");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DepRadarDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
