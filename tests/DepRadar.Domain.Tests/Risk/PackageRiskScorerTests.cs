using DepRadar.Domain.Packages;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Risk;

public sealed class PackageRiskScorerTests
{
    private static readonly PackageId Package = PackageId.Create("Sample");
    private static readonly SemVer Version = SemVer.Parse("1.0.0");

    [Fact]
    public void Clean_permissive_package_scores_full_marks()
    {
        var assessment = PackageRiskScorer.Assess(Input(license: "MIT", latest: "MIT"));

        assessment.Findings.ShouldBeEmpty();
        assessment.Score.Value.ShouldBe(100);
        assessment.Score.Level.ShouldBe(RiskLevel.None);
    }

    [Fact]
    public void License_shift_from_oss_to_commercial_is_high()
    {
        // The MediatR case: an OSS version while newer versions moved to a non-SPDX license.
        var assessment = PackageRiskScorer.Assess(Input(license: "Apache-2.0", latest: "LicenseRef-Commercial"));

        assessment.Findings.ShouldContain(f => f.Category == RiskCategory.LicenseShift && f.Level == RiskLevel.High);
    }

    [Fact]
    public void Strong_copyleft_is_flagged_high()
    {
        var assessment = PackageRiskScorer.Assess(Input(license: "GPL-3.0-only", latest: "GPL-3.0-only"));

        assessment.Findings.ShouldContain(f => f.Code == "COPYLEFT" && f.Level == RiskLevel.High);
        assessment.Score.Value.ShouldBe(70);
    }

    [Fact]
    public void Deprecation_is_flagged()
    {
        var assessment = PackageRiskScorer.Assess(Input(license: "MIT", latest: "MIT", deprecated: true));

        assessment.Findings.ShouldContain(f => f.Code == "DEPRECATED" && f.Category == RiskCategory.Maintenance);
    }

    [Fact]
    public void Vulnerability_severity_drives_the_finding_and_score()
    {
        var vulnerability = PackageVulnerability.Create(Package, Version, "GHSA-test", RiskLevel.Critical, "boom", "OSV");

        var assessment = PackageRiskScorer.Assess(Input(license: "MIT", latest: "MIT", vulnerabilities: [vulnerability]));

        assessment.Findings.ShouldContain(f => f.Category == RiskCategory.Security && f.Level == RiskLevel.Critical);
        assessment.Score.Level.ShouldBe(RiskLevel.Critical);
        assessment.Score.Value.ShouldBe(50);
    }

    [Fact]
    public void Archived_repository_is_flagged_high()
    {
        var assessment = PackageRiskScorer.Assess(Input(license: "MIT", latest: "MIT", archived: true));

        assessment.Findings.ShouldContain(f => f.Code == "ARCHIVED" && f.Level == RiskLevel.High);
    }

    [Fact]
    public void Stale_repository_is_flagged_medium()
    {
        var assessment = PackageRiskScorer.Assess(Input(license: "MIT", latest: "MIT", stale: true));

        assessment.Findings.ShouldContain(f => f.Code == "STALE" && f.Level == RiskLevel.Medium);
    }

    private static PackageRiskInput Input(
        string license,
        string latest,
        bool deprecated = false,
        bool archived = false,
        bool stale = false,
        IReadOnlyList<PackageVulnerability>? vulnerabilities = null) =>
        new(
            Package,
            Version,
            SpdxLicense.Create(license),
            SpdxLicense.Create(latest),
            deprecated,
            archived,
            stale,
            vulnerabilities ?? []);
}
