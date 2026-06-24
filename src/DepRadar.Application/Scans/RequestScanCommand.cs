using DepRadar.Application.Messaging;

namespace DepRadar.Application.Scans;

/// <summary>
/// Command: queue a transitive scan for a package. Returns immediately with a
/// <see cref="ScanDto"/> in <c>Queued</c> state; the worker pipeline does the work.
/// </summary>
/// <param name="PackageId">The root NuGet package id to scan.</param>
public sealed record RequestScanCommand(string PackageId) : IRequest<ScanDto>;
