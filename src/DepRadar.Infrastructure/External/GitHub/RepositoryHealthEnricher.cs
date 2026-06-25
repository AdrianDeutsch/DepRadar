using DepRadar.Application.Abstractions;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DepRadar.Infrastructure.External.GitHub;

/// <summary>
/// Resolves a package's source-repository URL (via deps.dev), fetches its health (via
/// GitHub) and records it on the package. Best-effort: any failure is logged and
/// swallowed so repository lookups never fail a scan.
/// </summary>
internal sealed class RepositoryHealthEnricher(
    IPackageMetadataSource metadataSource,
    IRepositoryHealthSource healthSource,
    IPackageRepository packageRepository,
    TimeProvider timeProvider,
    ILogger<RepositoryHealthEnricher> logger)
    : IRepositoryHealthEnricher
{
    /// <inheritdoc />
    public async Task EnrichAsync(PackageId package, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await metadataSource.GetAsync(package, cancellationToken);
            if (metadata?.SourceRepositoryUrl is not { } repositoryUrl)
            {
                return;
            }

            var health = await healthSource.GetAsync(repositoryUrl, cancellationToken);
            if (health is null)
            {
                return;
            }

            var entity = await packageRepository.GetAsync(package, cancellationToken);
            entity?.RecordRepositoryHealth(repositoryUrl, health.Archived, health.LastPushedAt, timeProvider.GetUtcNow());
        }
#pragma warning disable CA1031 // Best-effort enrichment must never fail the scan.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            logger.LogWarning(exception, "Repository-health enrichment failed for {Package}.", package.Original);
        }
    }
}
