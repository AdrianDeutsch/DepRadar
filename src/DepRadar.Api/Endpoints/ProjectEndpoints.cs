using DepRadar.Application.Messaging;
using DepRadar.Application.Projects;

namespace DepRadar.Api.Endpoints;

/// <summary>Endpoint for scanning a whole project file's direct dependencies.</summary>
internal static class ProjectEndpoints
{
    /// <summary>Registers the <c>/api/projects</c> endpoint group.</summary>
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/projects/scan", ScanAsync)
            .WithTags("Projects")
            .WithName("ScanProject")
            .WithSummary("Parses a .csproj / packages.lock.json and queues a scan per direct dependency.")
            .Accepts<string>("text/plain")
            .Produces<ProjectScanDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> ScanAsync(HttpRequest request, ISender sender, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var content = await reader.ReadToEndAsync(cancellationToken);

        var result = await sender.Send(new RequestProjectScanCommand(content), cancellationToken);
        return Results.Accepted(value: result);
    }
}
