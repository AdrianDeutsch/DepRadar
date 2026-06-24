namespace DepRadar.Domain.Packages;

/// <summary>Lifecycle state of a scan run.</summary>
public enum ScanStatus
{
    /// <summary>Created and waiting to be picked up by the ingestion pipeline.</summary>
    Queued = 0,

    /// <summary>Currently resolving and persisting the dependency graph.</summary>
    Running = 1,

    /// <summary>Finished successfully; the graph is persisted.</summary>
    Completed = 2,

    /// <summary>Aborted with an error.</summary>
    Failed = 3,
}
