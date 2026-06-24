using DepRadar.Application.Abstractions;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Deterministic <see cref="IDependencyGraphResolver"/> returning a fixed diamond
/// graph (root → B, root → C, B → C) so scan persistence and the recursive-closure
/// query can be tested without touching the NuGet API.
/// </summary>
internal sealed class StubGraphResolver : IDependencyGraphResolver
{
    public Task<ResolvedGraph?> ResolveAsync(PackageId root, CancellationToken cancellationToken)
    {
        var rootVersion = SemVer.Parse("1.0.0");
        var b = PackageId.Create("B");
        var bVersion = SemVer.Parse("2.0.0");
        var c = PackageId.Create("C");
        var cVersion = SemVer.Parse("3.0.0");

        var nodes = new List<ResolvedNode>
        {
            new(root, rootVersion, IsRoot: true),
            new(b, bVersion, IsRoot: false),
            new(c, cVersion, IsRoot: false),
        };

        var edges = new List<ResolvedEdge>
        {
            new(root, rootVersion, b, bVersion, "[2.0.0, )", IsDirect: true),
            new(root, rootVersion, c, cVersion, "[3.0.0, )", IsDirect: true),
            new(b, bVersion, c, cVersion, "[3.0.0, )", IsDirect: false),
        };

        return Task.FromResult<ResolvedGraph?>(new ResolvedGraph(root, rootVersion, nodes, edges, Truncated: false));
    }
}
