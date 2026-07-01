using DepRadar.Application.Abstractions;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Infrastructure.External;

/// <summary>
/// An <see cref="IDependencyGraphResolver"/> that returns a pre-built graph. Used for
/// lockfile scans, where the exact installed packages are already known and must NOT be
/// re-resolved — the lockfile is the graph. Feeding it through the resolver port keeps
/// the whole assessment pipeline (vulnerabilities, exploit intelligence, scoring)
/// unchanged.
/// </summary>
internal sealed class FixedGraphResolver(ResolvedGraph graph) : IDependencyGraphResolver
{
    /// <inheritdoc />
    public Task<ResolvedGraph?> ResolveAsync(PackageId root, SemVer? pinnedVersion, CancellationToken cancellationToken) =>
        Task.FromResult<ResolvedGraph?>(graph);
}
