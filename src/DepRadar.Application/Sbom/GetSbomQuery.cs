using DepRadar.Application.Messaging;

namespace DepRadar.Application.Sbom;

/// <summary>
/// Query: produce a CycloneDX SBOM (JSON) for a scanned package's transitive graph.
/// Returns <see langword="null"/> if the package has not been scanned.
/// </summary>
/// <param name="PackageId">The root package id.</param>
public sealed record GetSbomQuery(string PackageId) : IRequest<string?>;
