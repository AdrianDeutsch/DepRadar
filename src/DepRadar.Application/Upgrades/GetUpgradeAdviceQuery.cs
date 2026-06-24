using DepRadar.Application.Messaging;

namespace DepRadar.Application.Upgrades;

/// <summary>
/// Query: assess whether upgrading a package from one version to another is worth it,
/// using RAG over changelogs plus risk data. Versions default to the lowest/highest
/// stored. Returns <see langword="null"/> if the package has not been scanned.
/// </summary>
/// <param name="PackageId">The package id.</param>
/// <param name="From">Current version (optional; defaults to the lowest stored).</param>
/// <param name="To">Target version (optional; defaults to the highest stored).</param>
public sealed record GetUpgradeAdviceQuery(string PackageId, string? From, string? To) : IRequest<UpgradeAdviceDto?>;
