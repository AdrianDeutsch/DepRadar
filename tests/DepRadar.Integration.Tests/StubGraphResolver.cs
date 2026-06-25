using DepRadar.Application.Abstractions;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Deterministic <see cref="IDependencyGraphResolver"/> returning a fixed diamond
/// graph (root → B, root → C, B → C) with crafted license/deprecation facts so scan
/// persistence, the recursive-closure query AND risk scoring can be tested without
/// touching NuGet:
/// <list type="bullet">
///   <item>root: MIT now, but its latest version is a non-SPDX commercial license — a
///   license shift (the "MediatR case").</item>
///   <item>B: GPL-3.0-only (strong copyleft).</item>
///   <item>C: Apache-2.0 but deprecated.</item>
/// </list>
/// </summary>
internal sealed class StubGraphResolver : IDependencyGraphResolver
{
    public Task<ResolvedGraph?> ResolveAsync(PackageId root, SemVer? pinnedVersion, CancellationToken cancellationToken)
    {
        var rootVersion = pinnedVersion ?? SemVer.Parse("1.0.0");
        var b = PackageId.Create("B");
        var bVersion = SemVer.Parse("2.0.0");
        var c = PackageId.Create("C");
        var cVersion = SemVer.Parse("3.0.0");

        var nodes = new List<ResolvedNode>
        {
            new(root, rootVersion, IsRoot: true,
                License: "MIT", IsDeprecated: false,
                LatestStableVersion: SemVer.Parse("2.0.0"), LatestLicense: "FooCorp-Commercial-1.0"),
            new(b, bVersion, IsRoot: false,
                License: "GPL-3.0-only", IsDeprecated: false,
                LatestStableVersion: bVersion, LatestLicense: "GPL-3.0-only"),
            new(c, cVersion, IsRoot: false,
                License: "Apache-2.0", IsDeprecated: true,
                LatestStableVersion: cVersion, LatestLicense: "Apache-2.0"),
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
