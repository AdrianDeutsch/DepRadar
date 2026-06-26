using DepRadar.Application.Messaging;

namespace DepRadar.Application.Badges;

/// <summary>
/// Query: an SVG health badge for a package. Always returns an SVG (a neutral
/// "not scanned" badge when there is no scan), so a README image never 404s.
/// </summary>
/// <param name="PackageId">The root package id.</param>
public sealed record GetBadgeQuery(string PackageId) : IRequest<string>;
