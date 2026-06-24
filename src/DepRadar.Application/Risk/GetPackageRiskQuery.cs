using DepRadar.Application.Messaging;

namespace DepRadar.Application.Risk;

/// <summary>
/// Query: assess the risk of a single stored package (at its latest stored version).
/// Returns <see langword="null"/> if the package has not been scanned.
/// </summary>
/// <param name="PackageId">The package id.</param>
public sealed record GetPackageRiskQuery(string PackageId) : IRequest<PackageRiskDto?>;
