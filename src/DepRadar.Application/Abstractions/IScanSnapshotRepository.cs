using DepRadar.Domain.History;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Append-only store of per-scan risk snapshots, the history that drift detection
/// compares over time.
/// </summary>
public interface IScanSnapshotRepository
{
    /// <summary>Adds a snapshot. Commit via <see cref="IUnitOfWork"/>.</summary>
    Task AddAsync(ScanSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the most recent snapshots for a root, newest first (at most
    /// <paramref name="count"/>).
    /// </summary>
    Task<IReadOnlyList<ScanSnapshot>> GetRecentAsync(PackageId root, int count, CancellationToken cancellationToken);
}
