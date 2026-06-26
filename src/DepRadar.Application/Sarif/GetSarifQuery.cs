using DepRadar.Application.Messaging;

namespace DepRadar.Application.Sarif;

/// <summary>
/// Query: produce a SARIF 2.1.0 report of a scanned package's findings (for GitHub code
/// scanning). Returns <see langword="null"/> if the package has not been scanned.
/// </summary>
/// <param name="PackageId">The root package id.</param>
public sealed record GetSarifQuery(string PackageId) : IRequest<string?>;
