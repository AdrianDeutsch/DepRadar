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

    /// <summary>
    /// Retention: deletes all but the newest <paramref name="keep"/> snapshots for a
    /// root, keeping history bounded. Returns the number of rows removed.
    /// </summary>
    Task<int> PruneAsync(PackageId root, int keep, CancellationToken cancellationToken);

    /// <summary>Distinct roots that have at least one snapshot (the implicit watchlist).</summary>
    Task<IReadOnlyList<PackageId>> GetTrackedRootsAsync(CancellationToken cancellationToken);
}
