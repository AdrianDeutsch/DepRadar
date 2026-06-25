using DepRadar.Application.Messaging;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Sbom;

/// <summary>Handles <see cref="GetSbomQuery"/>: assembles the graph and renders a CycloneDX SBOM.</summary>
public sealed class GetSbomHandler(GraphAssessmentLoader loader, TimeProvider timeProvider)
    : IRequestHandler<GetSbomQuery, string?>
{
    /// <inheritdoc />
    public async Task<string?> Handle(GetSbomQuery request, CancellationToken cancellationToken)
    {
        var assessment = await loader.LoadAsync(PackageId.Create(request.PackageId), cancellationToken);
        return assessment is null ? null : CycloneDxBuilder.Build(assessment, timeProvider.GetUtcNow());
    }
}
