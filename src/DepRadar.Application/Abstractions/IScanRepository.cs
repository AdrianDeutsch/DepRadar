using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>Persistence port for the <see cref="Scan"/> aggregate.</summary>
public interface IScanRepository
{
    /// <summary>Adds a newly created scan (commit via <see cref="IUnitOfWork"/>).</summary>
    Task AddAsync(Scan scan, CancellationToken cancellationToken);

    /// <summary>Loads a scan by id, tracked for update, or <see langword="null"/>.</summary>
    Task<Scan?> GetAsync(ScanId id, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the ids of queued scans, oldest first, for the worker to process.
    /// </summary>
    Task<IReadOnlyList<ScanId>> GetQueuedAsync(int max, CancellationToken cancellationToken);

    /// <summary>Returns currently active scans (Queued or Running) for live progress.</summary>
    Task<IReadOnlyList<Scan>> GetActiveAsync(int max, CancellationToken cancellationToken);

    /// <summary>
    /// Returns tracked scans stuck in <c>Running</c> since before <paramref name="startedBefore"/>
    /// (abandoned by a crashed worker), so they can be requeued.
    /// </summary>
    Task<IReadOnlyList<Scan>> GetStaleRunningAsync(DateTimeOffset startedBefore, int max, CancellationToken cancellationToken);
}
