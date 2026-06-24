using DepRadar.Application.Abstractions;
using DepRadar.Application.Exceptions;
using DepRadar.Application.Messaging;
using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DepRadar.Application.Scans;

/// <summary>
/// Handles <see cref="RunScanCommand"/>: drives one scan from <c>Queued</c> to
/// <c>Completed</c> (or <c>Failed</c>). Resolves the transitive graph, maps it to the
/// domain and persists it idempotently. Already-finished scans are a no-op so the
/// worker can safely re-deliver.
/// </summary>
public sealed class RunScanHandler(
    IScanRepository scanRepository,
    IDependencyGraphResolver resolver,
    IGraphRepository graphRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<RunScanHandler> logger)
    : IRequestHandler<RunScanCommand, ScanDto>
{
    /// <inheritdoc />
    public async Task<ScanDto> Handle(RunScanCommand request, CancellationToken cancellationToken)
    {
        var scan = await scanRepository.GetAsync(ScanId.From(request.ScanId), cancellationToken)
            ?? throw new ScanNotFoundException(request.ScanId);

        if (scan.Status is ScanStatus.Completed or ScanStatus.Failed)
        {
            return ScanDto.FromDomain(scan);
        }

        scan.Start(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var graph = await resolver.ResolveAsync(scan.RootPackageId, cancellationToken);
            if (graph is null)
            {
                scan.Fail($"Package '{scan.RootPackageId}' was not found on NuGet.", timeProvider.GetUtcNow());
            }
            else
            {
                await PersistAsync(graph, cancellationToken);
                scan.Complete(graph.Nodes.Count, graph.Edges.Count, timeProvider.GetUtcNow());
                logger.LogInformation(
                    "Scan {ScanId} for {Root} completed: {Nodes} package(s), {Edges} edge(s){Truncated}.",
                    scan.Id.Value,
                    scan.RootPackageId.Original,
                    graph.Nodes.Count,
                    graph.Edges.Count,
                    graph.Truncated ? " (truncated)" : string.Empty);
            }
        }
#pragma warning disable CA1031 // Background-job boundary: any failure must mark the scan Failed, not crash the pipeline.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            logger.LogError(exception, "Scan {ScanId} failed.", scan.Id.Value);
            scan.Fail(exception.Message, timeProvider.GetUtcNow());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ScanDto.FromDomain(scan);
    }

    /// <summary>Maps the resolved graph to domain entities and persists it idempotently.</summary>
    private async Task PersistAsync(ResolvedGraph graph, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var packages = graph.Nodes
            .Select(node => Package.Create(node.Id, now))
            .ToList();

        var versions = graph.Nodes
            .Select(node => PackageVersion.Create(node.Id, node.Version))
            .ToList();

        var edges = graph.Edges
            .Select(edge => DependencyEdge.Create(
                edge.FromId,
                edge.FromVersion,
                edge.ToId,
                edge.ToVersion,
                edge.VersionRange,
                edge.IsDirect))
            .ToList();

        await graphRepository.UpsertGraphAsync(packages, versions, edges, cancellationToken);
    }
}
