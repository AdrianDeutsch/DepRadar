using DepRadar.Application.Abstractions;
using DepRadar.Application.Risk;
using DepRadar.Application.Sbom;
using DepRadar.Domain.Packages;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Sbom;

public sealed class CycloneDxBuilderTests
{
    [Fact]
    public void Builds_a_cyclonedx_bom_with_components_vulnerabilities_and_dependencies()
    {
        var package = PackageId.Create("Test.Pkg");
        var version = SemVer.Parse("1.0.0");
        var vulnerability = PackageVulnerability.Create(package, version, "GHSA-test", RiskLevel.High, "boom", "OSV");
        var input = new PackageRiskInput(package, version, SpdxLicense.Create("MIT"), SpdxLicense.Create("MIT"), false, false, false, [vulnerability]);
        var node = new AssessedNode(package, version, input, PackageRiskScorer.Assess(input));

        var edges = new List<GraphEdgeRow>
        {
            new("test.pkg", "1.0.0", "dep.a", "2.0.0", "[2.0.0, )", IsDirect: true, Depth: 1),
        };

        var json = CycloneDxBuilder.Build(new GraphAssessment(package, [node], edges), DateTimeOffset.UnixEpoch);

        json.ShouldContain("\"bomFormat\": \"CycloneDX\"");
        json.ShouldContain("\"specVersion\": \"1.5\"");
        json.ShouldContain("pkg:nuget/test.pkg@1.0.0");
        json.ShouldContain("\"id\": \"MIT\"");
        json.ShouldContain("GHSA-test");
        json.ShouldContain("\"dependsOn\"");
        json.ShouldContain("pkg:nuget/dep.a@2.0.0");
    }
}
