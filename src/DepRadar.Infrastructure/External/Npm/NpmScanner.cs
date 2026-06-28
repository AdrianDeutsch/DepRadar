using DepRadar.Application.Abstractions;
using DepRadar.Application.Analysis;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Infrastructure.External.Npm;

/// <summary>
/// Wires the npm resolver + npm vulnerability source into the same stateless
/// <see cref="ProjectAnalyzer"/> the NuGet path uses (with no-op metadata/repo-health,
/// since those signals are NuGet-specific), and exposes it as <see cref="INpmScanner"/>.
/// </summary>
internal sealed class NpmScanner(
    NpmDependencyGraphResolver resolver,
    NpmVulnerabilitySource vulnerabilities,
    TimeProvider timeProvider) : INpmScanner
{
    /// <inheritdoc />
    public Task<GraphAssessment?> ScanAsync(string package, string? version, CancellationToken cancellationToken)
    {
        var pinned = version is not null && SemVer.TryParse(version, out var parsed) ? parsed : null;
        var analyzer = new ProjectAnalyzer(resolver, vulnerabilities, NullMetadataSource.Instance, NullRepositoryHealthSource.Instance, timeProvider);
        return analyzer.AnalyzeAsync(PackageId.FromNormalized(package.Trim().ToLowerInvariant()), pinned, cancellationToken);
    }
}

/// <summary>No-op metadata source — npm has no deps.dev/NuGet catalog lookup.</summary>
internal sealed class NullMetadataSource : IPackageMetadataSource
{
    public static readonly NullMetadataSource Instance = new();

    public Task<PackageMetadata?> GetAsync(PackageId id, CancellationToken cancellationToken) =>
        Task.FromResult<PackageMetadata?>(null);
}

/// <summary>No-op repository-health source — skips root repo health for npm.</summary>
internal sealed class NullRepositoryHealthSource : IRepositoryHealthSource
{
    public static readonly NullRepositoryHealthSource Instance = new();

    public Task<RepositoryHealth?> GetAsync(Uri repositoryUrl, CancellationToken cancellationToken) =>
        Task.FromResult<RepositoryHealth?>(null);
}
