using DepRadar.Application.Messaging;
using DepRadar.Application.Risk;
using DepRadar.Application.Upgrades;

namespace DepRadar.Application.Reports;

/// <summary>
/// Handles <see cref="GetPackageReportQuery"/>: composes the existing graph-risk and
/// upgrade queries (via the mediator) into a Markdown report.
/// </summary>
public sealed class GetPackageReportHandler(ISender sender, TimeProvider timeProvider)
    : IRequestHandler<GetPackageReportQuery, string?>
{
    /// <inheritdoc />
    public async Task<string?> Handle(GetPackageReportQuery request, CancellationToken cancellationToken)
    {
        var risk = await sender.Send(new GetGraphRiskQuery(request.PackageId), cancellationToken);
        if (risk is null)
        {
            return null;
        }

        var upgrade = await sender.Send(new GetUpgradeAdviceQuery(request.PackageId, null, null), cancellationToken);

        return ReportBuilder.BuildMarkdown(risk, upgrade, timeProvider.GetUtcNow());
    }
}
