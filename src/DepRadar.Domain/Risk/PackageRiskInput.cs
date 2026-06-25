using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Domain.Risk;

/// <summary>
/// Everything the scorer needs about one package version, assembled from persisted
/// signals. Keeping it a plain input record makes <see cref="PackageRiskScorer"/> a
/// pure, fully unit-testable function.
/// </summary>
/// <param name="Package">The package being assessed.</param>
/// <param name="Version">The assessed (resolved) version.</param>
/// <param name="ResolvedLicense">The license of the assessed version, if known.</param>
/// <param name="LatestLicense">The license of the latest available version, if known.</param>
/// <param name="IsDeprecated">Whether the assessed version is deprecated.</param>
/// <param name="IsArchived">Whether the source repository is archived.</param>
/// <param name="IsRepositoryStale">Whether the source repository has had no recent commits.</param>
/// <param name="Vulnerabilities">Known advisories for the assessed version.</param>
public sealed record PackageRiskInput(
    PackageId Package,
    SemVer Version,
    SpdxLicense? ResolvedLicense,
    SpdxLicense? LatestLicense,
    bool IsDeprecated,
    bool IsArchived,
    bool IsRepositoryStale,
    IReadOnlyList<PackageVulnerability> Vulnerabilities);
