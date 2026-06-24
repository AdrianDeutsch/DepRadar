using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Abstractions;

/// <summary>
/// Resolves the transitive dependency graph of a package. Implemented in
/// Infrastructure against the NuGet registration API, with version ranges resolved
/// to concrete versions.
/// </summary>
public interface IDependencyGraphResolver
{
    /// <summary>
    /// Resolves the graph rooted at <paramref name="root"/> (at its latest stable
    /// version), or <see langword="null"/> if the package does not exist on NuGet.
    /// </summary>
    Task<ResolvedGraph?> ResolveAsync(PackageId root, CancellationToken cancellationToken);
}
