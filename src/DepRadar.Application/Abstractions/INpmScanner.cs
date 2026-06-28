using DepRadar.Application.Risk;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Scans an <c>npm</c> package's transitive graph and scores it — the multi-ecosystem
/// counterpart of the NuGet analyzer. Returns the same <see cref="GraphAssessment"/>, so
/// every downstream renderer (console, SBOM, policy) works unchanged.
/// </summary>
public interface INpmScanner
{
    /// <summary>
    /// Resolves and assesses <paramref name="package"/> at <paramref name="version"/>
    /// (or its latest), or <see langword="null"/> if it does not exist on the registry.
    /// </summary>
    Task<GraphAssessment?> ScanAsync(string package, string? version, CancellationToken cancellationToken);
}
