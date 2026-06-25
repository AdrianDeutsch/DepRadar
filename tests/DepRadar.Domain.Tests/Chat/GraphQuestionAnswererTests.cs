using DepRadar.Application.Chat;
using DepRadar.Application.Risk;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Chat;

public sealed class GraphQuestionAnswererTests
{
    [Fact]
    public void Answers_which_packages_are_deprecated()
    {
        var nodes = new[]
        {
            Node("Old.Pkg", new RiskFinding(RiskCategory.Maintenance, RiskLevel.High, "DEPRECATED", "deprecated")),
            Node("Fine.Pkg"),
        };

        var answer = GraphQuestionAnswerer.Answer("which packages are deprecated?", nodes);

        answer.Packages.ShouldBe(["Old.Pkg"]);
        answer.Text.ShouldContain("deprecated");
    }

    [Fact]
    public void Answers_which_packages_are_vulnerable()
    {
        var nodes = new[]
        {
            Node("Vuln.Pkg", new RiskFinding(RiskCategory.Security, RiskLevel.Critical, "VULN", "boom")),
            Node("Safe.Pkg"),
        };

        var answer = GraphQuestionAnswerer.Answer("any known CVEs?", nodes);

        answer.Packages.ShouldBe(["Vuln.Pkg"]);
    }

    [Fact]
    public void Identifies_the_riskiest_package()
    {
        var nodes = new[]
        {
            Node("Risky", new RiskFinding(RiskCategory.Security, RiskLevel.Critical, "VULN", "boom")),
            Node("Okay", new RiskFinding(RiskCategory.License, RiskLevel.Medium, "WEAK_COPYLEFT", "lgpl")),
        };

        var answer = GraphQuestionAnswerer.Answer("what is the riskiest package?", nodes);

        answer.Packages.ShouldBe(["Risky"]);
        answer.Text.ShouldContain("Risky");
    }

    private static AssessedNode Node(string id, params RiskFinding[] findings)
    {
        var package = PackageId.Create(id);
        var version = SemVer.Parse("1.0.0");
        var input = new PackageRiskInput(package, version, null, null, false, false, false, []);
        var assessment = new RiskAssessment(HealthScore.FromFindings(findings), findings);
        return new AssessedNode(package, version, input, assessment);
    }
}
