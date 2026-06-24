using DepRadar.Application.Abstractions;
using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DepRadar.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IPackageRepository"/>. Upserts are keyed by
/// identity so repeated scans update in place instead of duplicating rows.
/// </summary>
internal sealed class PackageRepository(DepRadarDbContext dbContext) : IPackageRepository
{
    /// <inheritdoc />
    public async Task<Package?> GetAsync(PackageId id, CancellationToken cancellationToken) =>
        // Tracked on purpose: the ingest use case mutates the returned aggregate.
        await dbContext.Packages.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PackageVersion>> GetVersionsAsync(PackageId id, CancellationToken cancellationToken) =>
        await dbContext.PackageVersions
            .AsNoTracking()
            .Where(v => v.PackageId == id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task UpsertAsync(Package package, IReadOnlyCollection<PackageVersion> versions, CancellationToken cancellationToken)
    {
        // A package loaded via GetAsync is already tracked (and mutated); a freshly
        // created one is detached and must be added.
        if (dbContext.Entry(package).State == EntityState.Detached)
        {
            await dbContext.Packages.AddAsync(package, cancellationToken);
        }

        var existingVersions = await dbContext.PackageVersions
            .Where(v => v.PackageId == package.Id)
            .Select(v => v.Version)
            .ToListAsync(cancellationToken);

        var existing = existingVersions.Select(v => v.ToString()).ToHashSet(StringComparer.Ordinal);

        foreach (var version in versions)
        {
            // Insert only versions we have not seen before. Mutable per-version
            // enrichment (license/deprecation changes) is handled from Slice 3.
            if (existing.Add(version.Version.ToString()))
            {
                await dbContext.PackageVersions.AddAsync(version, cancellationToken);
            }
        }
    }
}
