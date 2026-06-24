using DepRadar.Application.Messaging;

namespace DepRadar.Application.Packages;

/// <summary>
/// Query: read a previously ingested package from storage. Returns
/// <see langword="null"/> when the package has not been ingested yet.
/// </summary>
/// <param name="PackageId">The NuGet package id to read.</param>
public sealed record GetPackageQuery(string PackageId) : IRequest<PackageDto?>;
