using DepRadar.Application.Messaging;

namespace DepRadar.Application.Remediation;

/// <summary>
/// Query: the minimal safe upgrade for every vulnerable package in a scanned graph.
/// Returns <see langword="null"/> if the package has not been scanned.
/// </summary>
/// <param name="PackageId">The root package id.</param>
public sealed record GetRemediationsQuery(string PackageId) : IRequest<RemediationsDto?>;

/// <summary>The remediation plan for a scanned graph.</summary>
/// <param name="Root">The root package id.</param>
/// <param name="Remediations">One entry per vulnerable package.</param>
public sealed record RemediationsDto(string Root, IReadOnlyList<RemediationDto> Remediations);

/// <summary>A single suggested fix.</summary>
/// <param name="Package">The vulnerable package id.</param>
/// <param name="CurrentVersion">Its currently resolved version.</param>
/// <param name="SafeVersion">The smallest safe version to upgrade to, or <see langword="null"/> if none.</param>
/// <param name="Advisories">The advisories the upgrade clears.</param>
public sealed record RemediationDto(string Package, string CurrentVersion, string? SafeVersion, IReadOnlyList<string> Advisories);
