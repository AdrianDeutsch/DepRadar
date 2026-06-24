using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Packages;

/// <summary>
/// Handles <see cref="GetPackageQuery"/>: read a previously ingested package and
/// its versions from storage.
/// </summary>
public sealed class GetPackageHandler(IPackageRepository repository)
    : IRequestHandler<GetPackageQuery, PackageDto?>
{
    /// <inheritdoc />
    public async Task<PackageDto?> Handle(GetPackageQuery request, CancellationToken cancellationToken)
    {
        var id = PackageId.Create(request.PackageId);

        var package = await repository.GetAsync(id, cancellationToken);
        if (package is null)
        {
            return null;
        }

        var versions = await repository.GetVersionsAsync(id, cancellationToken);
        return PackageDto.FromDomain(package, versions);
    }
}
