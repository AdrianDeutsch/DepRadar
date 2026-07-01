using DepRadar.Application.Ecosystems;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Scans a <c>PyPI</c> (Python) package's transitive graph and scores it — the
/// multi-ecosystem counterpart of the NuGet analyzer. Returns the same
/// <see cref="GraphAssessment"/>, so every downstream renderer (console, SBOM, policy)
/// works unchanged.
/// </summary>
public interface IPyPiScanner
{
    /// <summary>
    /// Resolves and assesses <paramref name="package"/> at <paramref name="version"/> —
    /// an exact release, a PEP 440 specifier (<c>&gt;=2,&lt;3</c>, <c>~=1.4</c>, …), or
    /// null for the latest — or <see langword="null"/> if nothing on PyPI satisfies it.
    /// </summary>
    Task<GraphAssessment?> ScanAsync(string package, string? version, CancellationToken cancellationToken);

    /// <summary>All published final releases of <paramref name="package"/> (empty if unknown) — feeds upgrade-candidate selection.</summary>
    Task<IReadOnlyList<SemVer>> ListVersionsAsync(string package, CancellationToken cancellationToken);

    /// <summary>
    /// Assesses exact locked (name, version) pairs as one flat graph — the lockfile IS
    /// the resolution, so nothing is re-resolved. Entries whose version does not parse
    /// as a final PEP 440 release are skipped. Returns <see langword="null"/> when no
    /// entry is scannable.
    /// </summary>
    Task<GraphAssessment?> ScanLockedAsync(IReadOnlyList<LockedPackage> packages, CancellationToken cancellationToken);
}
