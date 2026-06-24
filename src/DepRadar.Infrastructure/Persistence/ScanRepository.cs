using DepRadar.Application.Abstractions;
using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DepRadar.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IScanRepository"/>.</summary>
internal sealed class ScanRepository(DepRadarDbContext dbContext) : IScanRepository
{
    /// <inheritdoc />
    public async Task AddAsync(Scan scan, CancellationToken cancellationToken) =>
        await dbContext.Scans.AddAsync(scan, cancellationToken);

    /// <inheritdoc />
    public async Task<Scan?> GetAsync(ScanId id, CancellationToken cancellationToken) =>
        await dbContext.Scans.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScanId>> GetQueuedAsync(int max, CancellationToken cancellationToken) =>
        await dbContext.Scans
            .AsNoTracking()
            .Where(s => s.Status == ScanStatus.Queued)
            .OrderBy(s => s.RequestedAt)
            .Take(max)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Scan>> GetActiveAsync(int max, CancellationToken cancellationToken) =>
        await dbContext.Scans
            .AsNoTracking()
            .Where(s => s.Status == ScanStatus.Queued || s.Status == ScanStatus.Running)
            .OrderByDescending(s => s.RequestedAt)
            .Take(max)
            .ToListAsync(cancellationToken);
}
