using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Graphs;

/// <summary>
/// Handles <see cref="GetPackageGraphQuery"/>: loads the transitive closure and
/// projects it into nodes + edges for the client.
/// </summary>
public sealed class GetPackageGraphHandler(
    IPackageRepository packageRepository,
    IGraphRepository graphRepository)
    : IRequestHandler<GetPackageGraphQuery, PackageGraphDto?>
{
    /// <inheritdoc />
    public async Task<PackageGraphDto?> Handle(GetPackageGraphQuery request, CancellationToken cancellationToken)
    {
        var rootId = PackageId.Create(request.PackageId);

        // Unknown package => 404; a known package with no dependencies => empty graph.
        if (await packageRepository.GetAsync(rootId, cancellationToken) is null)
        {
            return null;
        }

        var rows = await graphRepository.GetTransitiveClosureAsync(rootId, cancellationToken);

        var nodes = rows
            .SelectMany(row => new[]
            {
                (Id: row.DependentId, Version: row.DependentVersion),
                (Id: row.DependencyId, Version: row.DependencyVersion),
            })
            .Distinct()
            .Select(node => new GraphNodeDto(
                node.Id,
                node.Version,
                string.Equals(node.Id, rootId.Value, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var edges = rows
            .Select(row => new GraphEdgeDto(
                row.DependentId,
                row.DependentVersion,
                row.DependencyId,
                row.DependencyVersion,
                row.VersionRange,
                row.IsDirect,
                row.Depth))
            .ToList();

        return new PackageGraphDto(rootId.Original, Truncated: false, nodes, edges);
    }
}
