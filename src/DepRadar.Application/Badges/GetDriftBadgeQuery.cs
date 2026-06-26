using DepRadar.Application.Messaging;

namespace DepRadar.Application.Badges;

/// <summary>
/// Query: an SVG badge of a package's current drift status (clear / N issues / no
/// baseline). Always returns an SVG so a README image never 404s.
/// </summary>
/// <param name="PackageId">The root package id.</param>
public sealed record GetDriftBadgeQuery(string PackageId) : IRequest<string>;
