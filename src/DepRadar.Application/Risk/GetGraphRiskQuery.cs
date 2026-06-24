using DepRadar.Application.Messaging;

namespace DepRadar.Application.Risk;

/// <summary>
/// Query: assess the risk of a package's whole transitive graph. Returns
/// <see langword="null"/> if the package has not been scanned.
/// </summary>
/// <param name="PackageId">The root package id.</param>
public sealed record GetGraphRiskQuery(string PackageId) : IRequest<GraphRiskDto?>;
