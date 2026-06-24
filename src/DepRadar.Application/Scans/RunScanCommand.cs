using DepRadar.Application.Messaging;

namespace DepRadar.Application.Scans;

/// <summary>
/// Command: execute a queued scan — resolve the transitive graph and persist it.
/// Dispatched by the worker pipeline, one per dequeued scan id.
/// </summary>
/// <param name="ScanId">The id of the scan to run.</param>
public sealed record RunScanCommand(Guid ScanId) : IRequest<ScanDto>;
