using DepRadar.Application.Abstractions;
using DepRadar.Domain.History;
using DepRadar.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DepRadar.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IScanSnapshotRepository"/>.</summary>
internal sealed class ScanSnapshotRepository(DepRadarDbContext dbContext) : IScanSnapshotRepository
{
    /// <inheritdoc />
    public async Task AddAsync(ScanSnapshot snapshot, CancellationToken cancellationToken) =>
        await dbContext.ScanSnapshots.AddAsync(snapshot, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScanSnapshot>> GetRecentAsync(PackageId root, int count, CancellationToken cancellationToken) =>
        await dbContext.ScanSnapshots
            .AsNoTracking()
            .Where(s => s.RootPackageId == root)
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<int> PruneAsync(PackageId root, int keep, CancellationToken cancellationToken)
    {
        var keepIds = await dbContext.ScanSnapshots
            .Where(s => s.RootPackageId == root)
            .OrderByDescending(s => s.CreatedAt)
            .Take(keep)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        return await dbContext.ScanSnapshots
            .Where(s => s.RootPackageId == root && !keepIds.Contains(s.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PackageId>> GetTrackedRootsAsync(CancellationToken cancellationToken) =>
        await dbContext.ScanSnapshots
            .AsNoTracking()
            .Select(s => s.RootPackageId)
            .Distinct()
            .ToListAsync(cancellationToken);
}
