using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Risk;

/// <summary>Handles <see cref="GetPackageRiskQuery"/>: scores one package.</summary>
public sealed class GetPackageRiskHandler(
    IPackageRepository packageRepository,
    IRiskRepository riskRepository)
    : IRequestHandler<GetPackageRiskQuery, PackageRiskDto?>
{
    /// <inheritdoc />
    public async Task<PackageRiskDto?> Handle(GetPackageRiskQuery request, CancellationToken cancellationToken)
    {
        var packageId = PackageId.Create(request.PackageId);

        var versions = await packageRepository.GetVersionsAsync(packageId, cancellationToken);
        if (versions.Count == 0)
        {
            return null;
        }

        var assessedVersion = versions.Max(v => v.Version)!;

        var input = await riskRepository.GetRiskInputAsync(packageId, assessedVersion, cancellationToken);
        if (input is null)
        {
            return null;
        }

        return PackageRiskDto.FromAssessment(packageId, assessedVersion, PackageRiskScorer.Assess(input));
    }
}
