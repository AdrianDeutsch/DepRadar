using DepRadar.Application.Risk;

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
}
