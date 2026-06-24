using DepRadar.Application.Messaging;
using DepRadar.Application.Packages;

namespace DepRadar.Api.Endpoints;

/// <summary>
/// Maps the Slice 1 package endpoints. Endpoints stay thin: they translate HTTP to
/// a mediator request and back, with all behavior living in the application layer.
/// </summary>
internal static class PackageEndpoints
{
    /// <summary>Registers the <c>/api/packages</c> endpoint group.</summary>
    public static IEndpointRouteBuilder MapPackageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/packages").WithTags("Packages");

        group.MapPost("/{id}/scan", ScanPackageAsync)
            .WithName("ScanPackage")
            .WithSummary("Resolves a package from deps.dev and stores its metadata (idempotent).")
            .Produces<PackageDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id}", GetPackageAsync)
            .WithName("GetPackage")
            .WithSummary("Returns a previously ingested package.")
            .Produces<PackageDto>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ScanPackageAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var package = await sender.Send(new IngestPackageCommand(id), cancellationToken);
        return Results.Ok(package);
    }

    private static async Task<IResult> GetPackageAsync(string id, ISender sender, CancellationToken cancellationToken)
    {
        var package = await sender.Send(new GetPackageQuery(id), cancellationToken);
        return package is null ? Results.NotFound() : Results.Ok(package);
    }
}
