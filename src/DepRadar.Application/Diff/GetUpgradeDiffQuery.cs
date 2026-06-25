using DepRadar.Application.Messaging;

namespace DepRadar.Application.Diff;

/// <summary>
/// Query: compute the upgrade impact of moving <paramref name="PackageId"/> from
/// <paramref name="FromVersion"/> to <paramref name="ToVersion"/>. Resolves both
/// graphs live (no scan needed). Returns <see langword="null"/> when the package or
/// either version cannot be resolved.
/// </summary>
/// <param name="PackageId">The root package id.</param>
/// <param name="FromVersion">The baseline version.</param>
/// <param name="ToVersion">The target version.</param>
public sealed record GetUpgradeDiffQuery(string PackageId, string FromVersion, string ToVersion) : IRequest<UpgradeDiff?>;
