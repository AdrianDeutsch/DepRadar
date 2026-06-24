using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Port for fetching package metadata from an external registry. Implemented in
/// Infrastructure against deps.dev / the NuGet V3 API over a resilient HttpClient.
/// </summary>
public interface IPackageMetadataSource
{
    /// <summary>
    /// Fetches metadata for the given package, or <see langword="null"/> if the
    /// package is unknown to the source.
    /// </summary>
    Task<PackageMetadata?> GetAsync(PackageId id, CancellationToken cancellationToken);
}
