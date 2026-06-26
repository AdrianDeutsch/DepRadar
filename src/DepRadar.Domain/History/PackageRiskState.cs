using DepRadar.Domain.Risk;

namespace DepRadar.Domain.History;

/// <summary>
/// A compact, point-in-time snapshot of one package's risk state — exactly what drift
/// detection needs to compare two scans over time. Stored as part of a
/// <see cref="ScanSnapshot"/>; kept as plain strings/scalars so it serializes cleanly.
/// </summary>
/// <param name="Package">The package id (normalized).</param>
/// <param name="Version">The resolved version at snapshot time.</param>
/// <param name="Score">The health score (0–100).</param>
/// <param name="Level">The overall risk level.</param>
/// <param name="IsDeprecated">Whether the version was deprecated.</param>
/// <param name="IsArchived">Whether the source repository was archived.</param>
/// <param name="IsStale">Whether the source repository was stale.</param>
/// <param name="Advisories">Advisory ids affecting the version.</param>
/// <param name="License">The resolved SPDX license, if known.</param>
public sealed record PackageRiskState(
    string Package,
    string Version,
    int Score,
    RiskLevel Level,
    bool IsDeprecated,
    bool IsArchived,
    bool IsStale,
    IReadOnlyList<string> Advisories,
    string? License);
