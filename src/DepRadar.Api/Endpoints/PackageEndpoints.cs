using DepRadar.Application.Graphs;
using DepRadar.Application.Messaging;
using DepRadar.Application.Packages;
using DepRadar.Application.Scans;

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
}
