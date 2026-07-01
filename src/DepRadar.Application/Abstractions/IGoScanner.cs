using DepRadar.Application.Ecosystems;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Scans a Go module's dependency graph and scores it — the multi-ecosystem
/// counterpart of the NuGet analyzer. Returns the same <see cref="GraphAssessment"/>,
/// so every downstream renderer (console, SBOM, policy) works unchanged.
/// </summary>
public interface IGoScanner
{
    /// <summary>
    /// Resolves and assesses <paramref name="module"/> at <paramref name="version"/>
    /// (exact, with or without the <c>v</c> prefix; null for the latest release) — or
    /// <see langword="null"/> if the module proxy doesn't know it. Go requirements are
    /// exact (minimal version selection), so there is no range grammar to resolve.
    /// </summary>
    Task<GraphAssessment?> ScanAsync(string module, string? version, CancellationToken cancellationToken);

    /// <summary>All published versions of <paramref name="module"/> (empty if unknown) — feeds upgrade-candidate selection.</summary>
    Task<IReadOnlyList<SemVer>> ListVersionsAsync(string module, CancellationToken cancellationToken);

    /// <summary>
    /// Assesses exact locked (module, version) pairs as one flat graph — nothing is
    /// re-resolved. Entries whose version does not parse are skipped. Returns
    /// <see langword="null"/> when no entry is scannable.
    /// </summary>
    Task<GraphAssessment?> ScanLockedAsync(IReadOnlyList<LockedPackage> packages, CancellationToken cancellationToken);
}
