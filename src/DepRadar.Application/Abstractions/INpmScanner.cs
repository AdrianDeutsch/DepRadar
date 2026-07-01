using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Scans an <c>npm</c> package's transitive graph and scores it — the multi-ecosystem
/// counterpart of the NuGet analyzer. Returns the same <see cref="GraphAssessment"/>, so
/// every downstream renderer (console, SBOM, policy) works unchanged.
/// </summary>
public interface INpmScanner
{
    /// <summary>
    /// Resolves and assesses <paramref name="package"/> at <paramref name="version"/> —
    /// an exact version, an npm range (<c>^1.2.0</c>, <c>~1.2</c>, …), or null for the
    /// latest — or <see langword="null"/> if nothing on the registry satisfies it.
    /// </summary>
    Task<GraphAssessment?> ScanAsync(string package, string? version, CancellationToken cancellationToken);

    /// <summary>All published versions of <paramref name="package"/> (empty if unknown) — feeds upgrade-candidate selection.</summary>
    Task<IReadOnlyList<SemVer>> ListVersionsAsync(string package, CancellationToken cancellationToken);
}
