using DepRadar.Application.Messaging;
using DepRadar.Application.Risk;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Badges;

/// <summary>Handles <see cref="GetBadgeQuery"/>: renders a health badge from the assessed graph.</summary>
public sealed class GetBadgeHandler(GraphAssessmentLoader loader) : IRequestHandler<GetBadgeQuery, string>
{
    /// <inheritdoc />
    public async Task<string> Handle(GetBadgeQuery request, CancellationToken cancellationToken)
    {
        var assessment = await loader.LoadAsync(PackageId.Create(request.PackageId), cancellationToken);
        if (assessment is null || assessment.Nodes.Count == 0)
        {
            return BadgeRenderer.RenderUnknown();
        }

        var score = assessment.Nodes.Min(node => node.Assessment.Score.Value);
        var level = assessment.Nodes.Max(node => node.Assessment.Score.Level);
        return BadgeRenderer.RenderHealth(score, level);
    }
}
