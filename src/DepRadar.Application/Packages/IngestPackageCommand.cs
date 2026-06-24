using DepRadar.Application.Messaging;

namespace DepRadar.Application.Packages;

/// <summary>
/// Command: resolve a package's metadata from the external source and persist it
/// (insert or idempotent update), returning the stored state.
/// </summary>
/// <param name="PackageId">The NuGet package id to ingest, e.g. <c>"Newtonsoft.Json"</c>.</param>
public sealed record IngestPackageCommand(string PackageId) : IRequest<PackageDto>;
