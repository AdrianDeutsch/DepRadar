using DepRadar.Application.Abstractions;
using DepRadar.Application.Risk;
using DepRadar.Application.Sarif;
using DepRadar.Domain.Packages;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Sarif;

public sealed class SarifBuilderTests
{
    [Fact]
    public void Builds_a_sarif_report_with_rules_results_and_the_dependency_path()
    {
        var root = PackageId.Create("Root");
        var dep = PackageId.Create("Dep");
        var rootVersion = SemVer.Parse("1.0.0");
        var depVersion = SemVer.Parse("2.0.0");

        var vulnerability = PackageVulnerability.Create(dep, depVersion, "GHSA-x", RiskLevel.High, "boom", "OSV");
        var rootInput = new PackageRiskInput(root, rootVersion, SpdxLicense.Create("MIT"), SpdxLicense.Create("MIT"), false, false, false, []);
        var depInput = new PackageRiskInput(dep, depVersion, SpdxLicense.Create("MIT"), SpdxLicense.Create("MIT"), false, false, false, [vulnerability]);

        var graph = new GraphAssessment(
            root,
            [
                new AssessedNode(root, rootVersion, rootInput, PackageRiskScorer.Assess(rootInput)),
                new AssessedNode(dep, depVersion, depInput, PackageRiskScorer.Assess(depInput)),
            ],
            [new GraphEdgeRow("root", "1.0.0", "dep", "2.0.0", "[2.0.0, )", IsDirect: true, Depth: 1)]);

        var sarif = SarifBuilder.Build(graph, "MyApp.csproj");

        sarif.ShouldContain("\"version\": \"2.1.0\"");
        sarif.ShouldContain("\"name\": \"DepRadar\"");
        sarif.ShouldContain("\"ruleId\": \"VULN\"");
        sarif.ShouldContain("\"level\": \"error\"");
        sarif.ShouldContain("MyApp.csproj");
        sarif.ShouldContain("pulled in via root"); // the dependency path annotation
        sarif.ShouldContain("partialFingerprints");
    }
}
