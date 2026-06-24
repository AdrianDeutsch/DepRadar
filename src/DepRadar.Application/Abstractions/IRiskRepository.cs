using DepRadar.Domain.Packages;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Persistence port for risk data: stores advisories and assembles the
/// <see cref="PackageRiskInput"/>s the scorer needs from stored signals.
/// </summary>
public interface IRiskRepository
{
    /// <summary>Idempotently stores advisories (keyed by package, version, advisory id).</summary>
    Task UpsertVulnerabilitiesAsync(IReadOnlyCollection<PackageVulnerability> vulnerabilities, CancellationToken cancellationToken);

    /// <summary>
    /// Assembles the scoring input for one package version, or <see langword="null"/>
    /// if that version is not stored.
    /// </summary>
    Task<PackageRiskInput?> GetRiskInputAsync(PackageId package, SemVer version, CancellationToken cancellationToken);

    /// <summary>Assembles scoring inputs for a set of package versions (batched).</summary>
    Task<IReadOnlyList<PackageRiskInput>> GetRiskInputsAsync(
        IReadOnlyCollection<(PackageId Package, SemVer Version)> targets,
        CancellationToken cancellationToken);
}
