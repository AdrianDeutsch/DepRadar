using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Best-effort enrichment of a package's maintenance signals from its source
/// repository (resolves the repo URL, fetches health, records it on the package).
/// Implementations must not throw — a missing or unreachable repo simply yields no signal.
/// </summary>
public interface IRepositoryHealthEnricher
{
    /// <summary>Enriches the package's repository-health signals, if available.</summary>
    Task EnrichAsync(PackageId package, CancellationToken cancellationToken);
}
