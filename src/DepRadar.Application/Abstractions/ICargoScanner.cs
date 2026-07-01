using DepRadar.Application.Ecosystems;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Scans a <c>crates.io</c> (Rust) crate's transitive graph and scores it — the
/// multi-ecosystem counterpart of the NuGet analyzer. Returns the same
/// <see cref="GraphAssessment"/>, so every downstream renderer (console, SBOM, policy)
/// works unchanged.
/// </summary>
public interface ICargoScanner
{
    /// <summary>
    /// Resolves and assesses <paramref name="crate"/> at <paramref name="version"/> —
    /// an exact version, a Cargo requirement (<c>1.2</c>, <c>^1.2</c>, <c>~1.2</c>, …),
    /// or null for the latest — or <see langword="null"/> if nothing on crates.io
    /// satisfies it.
    /// </summary>
    Task<GraphAssessment?> ScanAsync(string crate, string? version, CancellationToken cancellationToken);

    /// <summary>All published versions of <paramref name="crate"/> (empty if unknown) — feeds upgrade-candidate selection.</summary>
    Task<IReadOnlyList<SemVer>> ListVersionsAsync(string crate, CancellationToken cancellationToken);

    /// <summary>
    /// Assesses exact locked (name, version) pairs as one flat graph — the lockfile IS
    /// the resolution, so nothing is re-resolved. Entries whose version does not parse
    /// are skipped. Returns <see langword="null"/> when no entry is scannable.
    /// </summary>
    Task<GraphAssessment?> ScanLockedAsync(IReadOnlyList<LockedPackage> packages, CancellationToken cancellationToken);
}
