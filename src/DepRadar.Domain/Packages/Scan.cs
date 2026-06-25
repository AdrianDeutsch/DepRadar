using DepRadar.Domain.Common;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Domain.Packages;

/// <summary>
/// A scan run: the unit of work that resolves and persists the transitive
/// dependency graph for one root package. Acts as the durable hand-off between the
/// API (which queues it) and the worker pipeline (which processes it), and as the
/// status the client polls.
/// </summary>
public sealed class Scan : IAggregateRoot
{
    // Parameterless constructor for the persistence layer (EF Core) only.
    private Scan()
    {
    }

    private Scan(ScanId id, PackageId rootPackageId, DateTimeOffset timestamp)
    {
        Id = id;
        RootPackageId = rootPackageId;
        Status = ScanStatus.Queued;
        RequestedAt = timestamp;
    }

    /// <summary>The scan identity.</summary>
    public ScanId Id { get; private set; }

    /// <summary>The root package whose graph is being resolved.</summary>
    public PackageId RootPackageId { get; private set; }

    /// <summary>Current lifecycle state.</summary>
    public ScanStatus Status { get; private set; }

    /// <summary>When the scan was requested (enqueued).</summary>
    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>When processing started, if it has.</summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>When the scan finished (completed or failed), if it has.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Number of distinct packages discovered in the graph.</summary>
    public int PackagesDiscovered { get; private set; }

    /// <summary>Number of dependency edges written.</summary>
    public int EdgesWritten { get; private set; }

    /// <summary>Failure reason when <see cref="Status"/> is <see cref="ScanStatus.Failed"/>.</summary>
    public string? Error { get; private set; }

    /// <summary>Queues a new scan for the given root package.</summary>
    public static Scan Create(PackageId rootPackageId, DateTimeOffset timestamp) =>
        new(ScanId.New(), rootPackageId, timestamp);

    /// <summary>Marks the scan as running. Idempotent for an already-running scan.</summary>
    /// <exception cref="InvalidOperationException">The scan is already finished.</exception>
    public void Start(DateTimeOffset timestamp)
    {
        EnsureNotFinished();
        Status = ScanStatus.Running;
        StartedAt = timestamp;
    }

    /// <summary>Marks the scan completed with the resulting graph size.</summary>
    public void Complete(int packagesDiscovered, int edgesWritten, DateTimeOffset timestamp)
    {
        Status = ScanStatus.Completed;
        PackagesDiscovered = packagesDiscovered;
        EdgesWritten = edgesWritten;
        CompletedAt = timestamp;
        Error = null;
    }

    /// <summary>Marks the scan failed with a reason.</summary>
    public void Fail(string error, DateTimeOffset timestamp)
    {
        Status = ScanStatus.Failed;
        Error = error;
        CompletedAt = timestamp;
    }

    /// <summary>
    /// Returns a stuck (e.g. abandoned by a crashed worker) scan to the queue so it can
    /// be retried. No-op if the scan already finished.
    /// </summary>
    public void Requeue()
    {
        if (Status is ScanStatus.Completed or ScanStatus.Failed)
        {
            return;
        }

        Status = ScanStatus.Queued;
        StartedAt = null;
    }

    private void EnsureNotFinished()
    {
        if (Status is ScanStatus.Completed or ScanStatus.Failed)
        {
            throw new InvalidOperationException($"Scan {Id} is already {Status} and cannot be restarted.");
        }
    }
}
