using DepRadar.Application.Messaging;
using DepRadar.Application.Scans;

namespace DepRadar.Api.Endpoints;

/// <summary>Scan status endpoints — the client polls these after queuing a scan.</summary>
internal static class ScanEndpoints
{
    /// <summary>Registers the <c>/api/scans</c> endpoint group.</summary>
    public static IEndpointRouteBuilder MapScanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scans").WithTags("Scans");

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetScan")
            .WithSummary("Returns the status and result counts of a scan.")
            .Produces<ScanDto>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetAsync(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var scan = await sender.Send(new GetScanQuery(id), cancellationToken);
        return scan is null ? Results.NotFound() : Results.Ok(scan);
    }
}
