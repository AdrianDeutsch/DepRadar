namespace DepRadar.Application.Diff;

/// <summary>
/// The impact of moving a package from one version to another: how the transitive
/// graph and the risk picture change. Answers "what do I take on if I upgrade?".
/// </summary>
/// <param name="Package">The root package.</param>
/// <param name="FromVersion">The baseline version.</param>
/// <param name="ToVersion">The target version.</param>
/// <param name="FromScore">Overall health score at the baseline (0–100).</param>
/// <param name="FromLevel">Overall risk level at the baseline.</param>
/// <param name="ToScore">Overall health score at the target.</param>
/// <param name="ToLevel">Overall risk level at the target.</param>
/// <param name="AddedPackages">Transitive packages present only after the upgrade.</param>
/// <param name="RemovedPackages">Transitive packages dropped by the upgrade.</param>
/// <param name="ChangedPackages">Shared packages resolved to a different version.</param>
/// <param name="NewAdvisories">Advisories introduced by the upgrade.</param>
/// <param name="ResolvedAdvisories">Advisories the upgrade clears.</param>
public sealed record UpgradeDiff(
    string Package,
    string FromVersion,
    string ToVersion,
    int FromScore,
    string FromLevel,
    int ToScore,
    string ToLevel,
    IReadOnlyList<string> AddedPackages,
    IReadOnlyList<string> RemovedPackages,
    IReadOnlyList<VersionChange> ChangedPackages,
    IReadOnlyList<string> NewAdvisories,
    IReadOnlyList<string> ResolvedAdvisories)
{
    /// <summary>The health-score change (positive = healthier after the upgrade).</summary>
    public int ScoreDelta => ToScore - FromScore;
}

/// <summary>A shared dependency that resolves to a different version after the upgrade.</summary>
public sealed record VersionChange(string Package, string FromVersion, string ToVersion);
