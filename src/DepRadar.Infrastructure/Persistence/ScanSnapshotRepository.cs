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
}
