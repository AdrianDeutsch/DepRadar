using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Risk;

/// <summary>API-facing risk assessment for a single package version.</summary>
public sealed record PackageRiskDto(
    string PackageId,
    string Version,
    int Score,
    string Level,
    IReadOnlyList<RiskFindingDto> Findings)
{
    /// <summary>Projects a domain <see cref="RiskAssessment"/> into a DTO.</summary>
    public static PackageRiskDto FromAssessment(PackageId package, SemVer version, RiskAssessment assessment) => new(
        package.Original,
        version.ToString(),
        assessment.Score.Value,
        assessment.Score.Level.ToString(),
        assessment.Findings.Select(RiskFindingDto.FromDomain).ToList());
}

/// <summary>API-facing risk finding.</summary>
public sealed record RiskFindingDto(string Category, string Level, string Code, string Message)
{
    /// <summary>Projects a domain <see cref="RiskFinding"/> into a DTO.</summary>
    public static RiskFindingDto FromDomain(RiskFinding finding) =>
        new(finding.Category.ToString(), finding.Level.ToString(), finding.Code, finding.Message);
}
