using System.Collections.Frozen;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Policy;

/// <summary>
/// A dependency-health gate: the thresholds a graph must satisfy to pass (e.g. in CI).
/// Deliberately small — the cases that actually fail a build: a too-high risk level,
/// deprecated packages, or disallowed license families.
/// </summary>
/// <param name="FailOn">Any package whose level is at or above this fails the gate.</param>
/// <param name="AllowDeprecated">When <see langword="false"/>, any deprecated package fails the gate.</param>
/// <param name="ForbiddenLicenses">License categories that fail the gate (e.g. Copyleft for proprietary apps).</param>
public sealed record RiskPolicy(
    RiskLevel FailOn,
    bool AllowDeprecated,
    IReadOnlySet<LicenseCategory> ForbiddenLicenses)
{
    /// <summary>
    /// Package ids whose violations are suppressed — accepted risk (VEX-style), still
    /// shown in the report but not failing the gate.
    /// </summary>
    public IReadOnlySet<string> IgnoredPackages { get; init; } = FrozenSet<string>.Empty;

    /// <summary>A lenient default: fail only on Critical, allow everything else.</summary>
    public static RiskPolicy Default { get; } =
        new(RiskLevel.Critical, AllowDeprecated: true, FrozenSet<LicenseCategory>.Empty);
}
