using DepRadar.Application.Reports;
using DepRadar.Application.Risk;
using DepRadar.Application.Upgrades;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Reports;

public sealed class ReportBuilderTests
{
    [Fact]
    public void Builds_a_markdown_report_with_summary_ranking_and_upgrade()
    {
        var risk = new GraphRiskDto("Root.Package", 40, "Critical", 2,
        [
            new PackageRiskDto("b", "2.0.0", 20, "Critical",
                [new RiskFindingDto("Security", "Critical", "VULN", "boom")]),
            new PackageRiskDto("root.package", "1.0.0", 70, "High", []),
        ]);

        var upgrade = new UpgradeAdviceDto("Root.Package", "1.0.0", "2.0.0", "Caution", "Upgrade with care.", ["High Security: boom"], false, "prompt");

        var markdown = ReportBuilder.BuildMarkdown(risk, upgrade, DateTimeOffset.UnixEpoch);

        markdown.ShouldContain("# DepRadar report — Root.Package");
        markdown.ShouldContain("**Project health:** 40/100 (Critical)");
        markdown.ShouldContain("## Risk ranking");
        markdown.ShouldContain("| b | 2.0.0 | 20 | Critical | VULN (Critical) |");
        markdown.ShouldContain("**Recommendation:** Caution");
        markdown.ShouldContain("Upgrade with care.");
    }

    [Fact]
    public void Omits_upgrade_section_when_no_advice()
    {
        var risk = new GraphRiskDto("Solo", 100, "None", 1,
            [new PackageRiskDto("solo", "1.0.0", 100, "None", [])]);

        var markdown = ReportBuilder.BuildMarkdown(risk, upgrade: null, DateTimeOffset.UnixEpoch);

        markdown.ShouldContain("Solo");
        markdown.ShouldNotContain("## Upgrade advice");
    }
}
