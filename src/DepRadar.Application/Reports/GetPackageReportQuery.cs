using DepRadar.Application.Messaging;

namespace DepRadar.Application.Reports;

/// <summary>
/// Query: build an audit-ready Markdown report for a scanned package. Returns
/// <see langword="null"/> if the package has not been scanned.
/// </summary>
/// <param name="PackageId">The root package id.</param>
public sealed record GetPackageReportQuery(string PackageId) : IRequest<string?>;
