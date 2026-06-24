namespace DepRadar.Application.Risk;

/// <summary>
/// Project-level risk: the worst-case score across the transitive graph plus the
/// per-package breakdown, worst first. This is the "audit" view.
/// </summary>
public sealed record GraphRiskDto(
    string Root,
    int OverallScore,
    string OverallLevel,
    int PackagesAssessed,
    IReadOnlyList<PackageRiskDto> Packages);
