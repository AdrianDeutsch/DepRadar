using DepRadar.Application.Messaging;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Sarif;

/// <summary>Handles <see cref="GetSarifQuery"/>: assembles the graph and renders SARIF.</summary>
public sealed class GetSarifHandler(GraphAssessmentLoader loader) : IRequestHandler<GetSarifQuery, string?>
{
    /// <inheritdoc />
    public async Task<string?> Handle(GetSarifQuery request, CancellationToken cancellationToken)
    {
        var root = PackageId.Create(request.PackageId);
        var assessment = await loader.LoadAsync(root, cancellationToken);
        return assessment is null ? null : SarifBuilder.Build(assessment, root.Value);
    }
}
