using DepRadar.Application.History;
using DepRadar.Application.Messaging;

namespace DepRadar.Api.Endpoints;

/// <summary>Cross-package drift endpoints (not scoped to a single package id).</summary>
internal static class DriftEndpoints
{
    /// <summary>Registers the <c>/api/drift</c> endpoint group.</summary>
    public static IEndpointRouteBuilder MapDriftEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/drift/digest", GetDigestAsync)
            .WithTags("Drift")
            .WithName("GetDriftDigest")
            .WithSummary("A Markdown digest of what changed across every tracked package since the previous scan.")
            .Produces(StatusCodes.Status200OK, contentType: "text/markdown");

        return app;
    }

    private static async Task<IResult> GetDigestAsync(ISender sender, CancellationToken cancellationToken)
    {
        var markdown = await sender.Send(new GetDriftDigestQuery(), cancellationToken);
        return Results.Text(markdown, "text/markdown");
    }
}
