using DepRadar.Application.Messaging;

namespace DepRadar.Application.Graphs;

/// <summary>
/// Query: read the transitive dependency graph rooted at a package. Returns
/// <see langword="null"/> if the package has never been scanned/stored.
/// </summary>
/// <param name="PackageId">The root package id.</param>
public sealed record GetPackageGraphQuery(string PackageId) : IRequest<PackageGraphDto?>;
