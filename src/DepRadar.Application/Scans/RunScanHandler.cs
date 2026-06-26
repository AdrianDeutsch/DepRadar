using DepRadar.Application.Abstractions;
using DepRadar.Application.Exceptions;
using DepRadar.Application.History;
using DepRadar.Application.Messaging;
using DepRadar.Application.Observability;
using DepRadar.Application.Risk;
using DepRadar.Domain.History;
using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DepRadar.Application.Scans;

/// <summary>
/// Handles <see cref="RunScanCommand"/>: drives one scan from <c>Queued</c> to
/// <c>Completed</c> (or <c>Failed</c>). Resolves the transitive graph, persists it
/// idempotently together with per-version license/deprecation, and assesses each
/// node for known vulnerabilities. Already-finished scans are a no-op so the worker
/// can safely re-deliver.
/// </summary>
public sealed class RunScanHandler(
    IScanRepository scanRepository,
    IDependencyGraphResolver resolver,
    IGraphRepository graphRepository,
    IVulnerabilitySource vulnerabilitySource,
    IRiskRepository riskRepository,
    IChangelogIndexer changelogIndexer,
    IRepositoryHealthEnricher repositoryHealthEnricher,
    GraphAssessmentLoader assessmentLoader,
    IScanSnapshotRepository snapshots,
    IDriftNotifier driftNotifier,
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

        using var activity = DepRadarTelemetry.ActivitySource.StartActivity("scan");
        activity?.SetTag("depradar.package", scan.RootPackageId.Original);

        scan.Start(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var graph = await resolver.ResolveAsync(scan.RootPackageId, pinnedVersion: null, cancellationToken);
            if (graph is null)
            {
                scan.Fail($"Package '{scan.RootPackageId}' was not found on NuGet.", timeProvider.GetUtcNow());
            }
            else
            {
                await PersistGraphAsync(graph, cancellationToken);
                await AssessVulnerabilitiesAsync(graph, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                // Build the RAG corpus from what we just learned (versions, license
                // changes, advisories). Reads back the committed data above.
                await changelogIndexer.IndexAsync(scan.RootPackageId, cancellationToken);

                // Best-effort: enrich the root's maintenance signals from its repo.
                await repositoryHealthEnricher.EnrichAsync(scan.RootPackageId, cancellationToken);

                scan.Complete(graph.Nodes.Count, graph.Edges.Count, timeProvider.GetUtcNow());
                DepRadarTelemetry.ScansCompleted.Add(1);
                DepRadarTelemetry.PackagesDiscovered.Add(graph.Nodes.Count);
                activity?.SetTag("depradar.nodes", graph.Nodes.Count);
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

        // History: record a snapshot of the completed scan so drift can be detected
        // against the next one. Best-effort — a snapshot failure must not fail the scan.
        if (scan.Status == ScanStatus.Completed)
        {
            await RecordSnapshotAsync(scan.RootPackageId, cancellationToken);
        }

        return ScanDto.FromDomain(scan);
    }

    /// <summary>Captures the assessed graph as an append-only snapshot for drift detection.</summary>
    private async Task RecordSnapshotAsync(PackageId root, CancellationToken cancellationToken)
    {
#pragma warning disable CA1031 // Best-effort: drift history must never break the scan pipeline.
        try
        {
            var assessment = await assessmentLoader.LoadAsync(root, cancellationToken);
            if (assessment is null)
            {
                return;
            }

            await snapshots.AddAsync(SnapshotFactory.From(assessment, timeProvider.GetUtcNow()), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // Note: history is bounded by the worker's SnapshotRetentionService, not here,
            // so the scan path stays lean.
            await AlertOnDriftAsync(root, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to record a drift snapshot for {Root}.", root.Original);
        }
#pragma warning restore CA1031
    }

    /// <summary>Compares the two latest snapshots and pushes an alert when high-severity risk newly appears.</summary>
    private async Task AlertOnDriftAsync(PackageId root, CancellationToken cancellationToken)
    {
        var recent = await snapshots.GetRecentAsync(root, 2, cancellationToken);
        if (recent.Count < 2)
        {
            return;
        }

        var drift = DriftAnalyzer.Compare(recent[1], recent[0]);
        if (DriftAlert.Actionable(drift).Count > 0)
        {
            DepRadarTelemetry.MarkDrift(root.Value);
            await driftNotifier.NotifyAsync(drift, cancellationToken);
        }
        else
        {
            // Drift has cleared: let channels close any alert they opened for this package.
            DepRadarTelemetry.ClearDrift(root.Value);
            await driftNotifier.ResolveAsync(root, cancellationToken);
        }
    }

    /// <summary>Maps the resolved graph to domain entities and persists it idempotently.</summary>
    private async Task PersistGraphAsync(ResolvedGraph graph, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var packages = graph.Nodes
            .Select(node =>
            {
                var package = Package.Create(node.Id, now);
                package.Refresh(
                    description: null,
                    projectUrl: null,
                    sourceRepositoryUrl: null,
                    license: ParseLicense(node.LatestLicense),
                    isDeprecated: node.IsDeprecated,
                    latestStableVersion: node.LatestStableVersion,
                    timestamp: now);
                return package;
            })
            .ToList();

        var versions = graph.Nodes
            .Select(node => PackageVersion.Create(
                node.Id,
                node.Version,
                publishedAt: null,
                isDeprecated: node.IsDeprecated,
                license: ParseLicense(node.License)))
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

    /// <summary>Queries the vulnerability source for every node and stores the advisories.</summary>
    private async Task AssessVulnerabilitiesAsync(ResolvedGraph graph, CancellationToken cancellationToken)
    {
        var vulnerabilities = new List<PackageVulnerability>();
        foreach (var node in graph.Nodes)
        {
            var advisories = await vulnerabilitySource.GetAsync(node.Id, node.Version, cancellationToken);
            vulnerabilities.AddRange(advisories.Select(advisory => PackageVulnerability.Create(
                node.Id,
                node.Version,
                advisory.AdvisoryId,
                advisory.Severity,
                advisory.Summary,
                advisory.Source)));
        }

        if (vulnerabilities.Count > 0)
        {
            await riskRepository.UpsertVulnerabilitiesAsync(vulnerabilities, cancellationToken);
        }
    }

    private static SpdxLicense? ParseLicense(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : SpdxLicense.Create(raw);
}
