using DepRadar.Application.Abstractions;
using DepRadar.Application.Exceptions;
using DepRadar.Application.Messaging;
using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DepRadar.Application.Packages;

/// <summary>
/// Handles <see cref="IngestPackageCommand"/>: fetch external metadata, map it into
/// the domain and upsert it. The whole operation is idempotent — re-running it for
/// the same package updates in place instead of duplicating.
/// </summary>
public sealed class IngestPackageHandler(
    IPackageMetadataSource metadataSource,
    IPackageRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<IngestPackageHandler> logger)
    : IRequestHandler<IngestPackageCommand, PackageDto>
{
    /// <inheritdoc />
    public async Task<PackageDto> Handle(IngestPackageCommand request, CancellationToken cancellationToken)
    {
        var id = PackageId.Create(request.PackageId);

        var metadata = await metadataSource.GetAsync(id, cancellationToken)
            ?? throw new PackageNotFoundException(id);

        var now = timeProvider.GetUtcNow();
        var package = await repository.GetAsync(id, cancellationToken) ?? Package.Create(id, now);

        package.Refresh(
            metadata.Description,
            metadata.ProjectUrl,
            metadata.SourceRepositoryUrl,
            ParseLicense(metadata.License),
            metadata.IsDeprecated,
            ParseVersion(metadata.LatestStableVersion),
            now);

        var versions = MapVersions(id, metadata.Versions);

        await repository.UpsertAsync(package, versions, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Ingested package {PackageId} with {VersionCount} version(s).",
            id.Original,
            versions.Count);

        return PackageDto.FromDomain(package, versions);
    }

    private List<PackageVersion> MapVersions(PackageId id, IReadOnlyList<PackageVersionMetadata> source)
    {
        var versions = new List<PackageVersion>(source.Count);
        foreach (var version in source)
        {
            // External data is untrusted: skip versions that do not parse instead
            // of failing the entire ingest.
            if (!SemVer.TryParse(version.Version, out var semVer))
            {
                logger.LogWarning("Skipping unparseable version '{Version}' for {PackageId}.", version.Version, id.Original);
                continue;
            }

            versions.Add(PackageVersion.Create(
                id,
                semVer,
                version.PublishedAt,
                version.IsDeprecated,
                ParseLicense(version.License)));
        }

        return versions;
    }

    private static SemVer? ParseVersion(string? raw) =>
        SemVer.TryParse(raw, out var version) ? version : null;

    private static SpdxLicense? ParseLicense(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : SpdxLicense.Create(raw);
}
