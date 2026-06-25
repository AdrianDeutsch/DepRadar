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
    /// Resolves the graph rooted at <paramref name="root"/>. When
    /// <paramref name="pinnedVersion"/> is supplied that exact root version is used
    /// (for upgrade-impact diffs); otherwise the latest stable version is chosen.
    /// Returns <see langword="null"/> if the package — or the pinned version — does
    /// not exist on NuGet.
    /// </summary>
    Task<ResolvedGraph?> ResolveAsync(PackageId root, SemVer? pinnedVersion, CancellationToken cancellationToken);
}
