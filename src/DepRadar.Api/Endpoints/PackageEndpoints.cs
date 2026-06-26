using DepRadar.Application.Badges;
using DepRadar.Application.Chat;
using DepRadar.Application.Diff;
using DepRadar.Application.Graphs;
using DepRadar.Application.History;
using DepRadar.Application.Messaging;
using DepRadar.Application.Packages;
using DepRadar.Application.Reports;
using DepRadar.Application.Risk;
using DepRadar.Application.Sbom;
using DepRadar.Application.Scans;
using DepRadar.Application.Upgrades;

namespace DepRadar.Api.Endpoints;

/// <summary>
/// Package endpoints. Endpoints stay thin: they translate HTTP to a mediator
/// request and back, with all behavior living in the application layer.
/// </summary>
internal static class PackageEndpoints
{
    /// <summary>Registers the <c>/api/packages</c> endpoint group.</summary>
    public static IEndpointRouteBuilder MapPackageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/packages").WithTags("Packages");

        group.MapPost("/{id}/scan", ScanAsync)
            .WithName("ScanPackage")
            .WithSummary("Queues a transitive dependency scan and returns 202 with the scan location.")
            .Produces<ScanDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id}/ingest", IngestAsync)
            .WithName("IngestPackage")
            .WithSummary("Synchronously resolves a single package's metadata from deps.dev and stores it.")
            .Produces<PackageDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id}", GetAsync)
            .WithName("GetPackage")
            .WithSummary("Returns a stored package.")
            .Produces<PackageDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/graph", GetGraphAsync)
            .WithName("GetPackageGraph")
            .WithSummary("Returns the transitive dependency graph for a scanned package.")
            .Produces<PackageGraphDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/risk", GetRiskAsync)
            .WithName("GetPackageRisk")
            .WithSummary("Returns the health score and findings for a package.")
            .Produces<PackageRiskDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/graph/risk", GetGraphRiskAsync)
            .WithName("GetPackageGraphRisk")
            .WithSummary("Returns the project-level risk across the transitive graph (worst first).")
            .Produces<GraphRiskDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/upgrade", GetUpgradeAsync)
            .WithName("GetUpgradeAdvice")
            .WithSummary("Assesses an upgrade (RAG over changelogs + risk); ?from= & ?to= optional.")
            .Produces<UpgradeAdviceDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/report", GetReportAsync)
            .WithName("GetPackageReport")
            .WithSummary("Returns an audit-ready Markdown report for a scanned package.")
            .Produces(StatusCodes.Status200OK, contentType: "text/markdown")
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/sbom", GetSbomAsync)
            .WithName("GetPackageSbom")
            .WithSummary("Returns a CycloneDX 1.5 SBOM (components, licenses, vulnerabilities, dependencies).")
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.cyclonedx+json")
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id}/chat", AskAsync)
            .WithName("AskGraphQuestion")
            .WithSummary("Answers a natural-language question about a scanned package's graph.")
            .Produces<ChatAnswerDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/diff", GetDiffAsync)
            .WithName("GetUpgradeDiff")
            .WithSummary("Diffs the resolved graph and risk between two versions (upgrade impact).")
            .Produces<UpgradeDiff>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/drift", GetDriftAsync)
            .WithName("GetDrift")
            .WithSummary("How the package's dependency health drifted since the previous scan.")
            .Produces<DriftReportDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/badge.svg", GetBadgeAsync)
            .WithName("GetBadge")
            .WithSummary("A shields-style SVG health badge for embedding in a README.")
            .Produces(StatusCodes.Status200OK, contentType: "image/svg+xml");

        return app;
    }

    private static async Task<IResult> ScanAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var scan = await sender.Send(new RequestScanCommand(id), cancellationToken);
        return Results.Accepted($"/api/scans/{scan.Id}", scan);
    }

    private static async Task<IResult> IngestAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var package = await sender.Send(new IngestPackageCommand(id), cancellationToken);
        return Results.Ok(package);
    }

    private static async Task<IResult> GetAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var package = await sender.Send(new GetPackageQuery(id), cancellationToken);
        return package is null ? Results.NotFound() : Results.Ok(package);
    }

    private static async Task<IResult> GetGraphAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var graph = await sender.Send(new GetPackageGraphQuery(id), cancellationToken);
        return graph is null ? Results.NotFound() : Results.Ok(graph);
    }

    private static async Task<IResult> GetRiskAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var risk = await sender.Send(new GetPackageRiskQuery(id), cancellationToken);
        return risk is null ? Results.NotFound() : Results.Ok(risk);
    }

    private static async Task<IResult> GetGraphRiskAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var risk = await sender.Send(new GetGraphRiskQuery(id), cancellationToken);
        return risk is null ? Results.NotFound() : Results.Ok(risk);
    }

    private static async Task<IResult> GetUpgradeAsync(string id, string? from, string? to, ISender sender, CancellationToken cancellationToken)
    {
        var advice = await sender.Send(new GetUpgradeAdviceQuery(id, from, to), cancellationToken);
        return advice is null ? Results.NotFound() : Results.Ok(advice);
    }

    private static async Task<IResult> GetReportAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var markdown = await sender.Send(new GetPackageReportQuery(id), cancellationToken);
        return markdown is null ? Results.NotFound() : Results.Text(markdown, "text/markdown");
    }

    private static async Task<IResult> GetSbomAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var sbom = await sender.Send(new GetSbomQuery(id), cancellationToken);
        return sbom is null ? Results.NotFound() : Results.Text(sbom, "application/vnd.cyclonedx+json");
    }

    private static async Task<IResult> AskAsync(string id, ChatRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var answer = await sender.Send(new AskGraphQuestionQuery(id, request.Question), cancellationToken);
        return answer is null ? Results.NotFound() : Results.Ok(answer);
    }

    private static async Task<IResult> GetDiffAsync(string id, string? from, string? to, ISender sender, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return Results.BadRequest("Both 'from' and 'to' version query parameters are required.");
        }

        var diff = await sender.Send(new GetUpgradeDiffQuery(id, from, to), cancellationToken);
        return diff is null ? Results.NotFound() : Results.Ok(diff);
    }

    private static async Task<IResult> GetDriftAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var drift = await sender.Send(new GetDriftQuery(id), cancellationToken);
        return drift is null ? Results.NotFound() : Results.Ok(drift);
    }

    private static async Task<IResult> GetBadgeAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var svg = await sender.Send(new GetBadgeQuery(id), cancellationToken);
        return Results.Text(svg, "image/svg+xml");
    }
}
