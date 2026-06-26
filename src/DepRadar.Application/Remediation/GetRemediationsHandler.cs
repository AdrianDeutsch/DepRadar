using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Remediation;

/// <summary>
/// Handles <see cref="GetRemediationsQuery"/>: for every vulnerable node, reads each
/// advisory's patched version from OSV (by advisory id) and picks the smallest upgrade
/// that clears them all.
/// </summary>
public sealed class GetRemediationsHandler(GraphAssessmentLoader loader, IVulnerabilitySource vulnerabilities)
    : IRequestHandler<GetRemediationsQuery, RemediationsDto?>
{
    /// <inheritdoc />
    public async Task<RemediationsDto?> Handle(GetRemediationsQuery request, CancellationToken cancellationToken)
    {
        var root = PackageId.Create(request.PackageId);
        var assessment = await loader.LoadAsync(root, cancellationToken);
        if (assessment is null)
        {
            return null;
        }

        var remediations = new List<RemediationDto>();
        foreach (var node in assessment.Nodes.Where(node => node.Input.Vulnerabilities.Count > 0))
        {
            var advisories = new List<string>();
            var fixedVersions = new List<string?>();
            foreach (var vulnerability in node.Input.Vulnerabilities)
            {
                advisories.Add(vulnerability.AdvisoryId);
                fixedVersions.Add(await vulnerabilities.GetFixedVersionAsync(
                    vulnerability.AdvisoryId, node.Package, node.Version, cancellationToken));
            }

            var safe = RemediationCalculator.SafeVersion(fixedVersions);
            remediations.Add(new RemediationDto(
                node.Package.Value,
                node.Version.ToString(),
                safe,
                advisories.Distinct(StringComparer.Ordinal).ToList()));
        }

        return new RemediationsDto(root.Value, remediations);
    }
}
