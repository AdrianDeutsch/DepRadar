using DepRadar.Application.Diff;
using DepRadar.Application.Risk;
using DepRadar.Domain.Packages;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Diff;

public sealed class GraphDifferTests
{
    [Fact]
    public void Reports_added_changed_and_new_advisories_when_upgrading_into_risk()
    {
        var from = Graph("root",
            Node("root", "1.0.0"),
            Node("dep.a", "1.0.0"));

        var to = Graph("root",
            Node("root", "2.0.0"),
            Node("dep.a", "2.0.0"),
            Node("dep.b", "1.0.0", vulnerabilities: [Vuln("dep.b", "1.0.0", "GHSA-bbbb")]));

        var diff = GraphDiffer.Diff(from, to);

        diff.FromVersion.ShouldBe("1.0.0");
        diff.ToVersion.ShouldBe("2.0.0");
        diff.AddedPackages.ShouldContain("dep.b@1.0.0");
        diff.RemovedPackages.ShouldBeEmpty();
        diff.ChangedPackages.ShouldContain(c => c.Package == "dep.a" && c.FromVersion == "1.0.0" && c.ToVersion == "2.0.0");
        diff.NewAdvisories.ShouldContain("GHSA-bbbb in dep.b@1.0.0");
        diff.ScoreDelta.ShouldBeLessThan(0); // upgrading pulls in a vulnerable transitive dependency
    }

    [Fact]
    public void Detects_a_cleared_advisory_and_dropped_dependency_when_upgrading_out_of_risk()
    {
        var from = Graph("root",
            Node("root", "1.0.0", vulnerabilities: [Vuln("root", "1.0.0", "GHSA-aaaa")]),
            Node("legacy.dep", "1.0.0"));

        var to = Graph("root",
            Node("root", "2.0.0"));

        var diff = GraphDiffer.Diff(from, to);

        diff.ResolvedAdvisories.ShouldContain("GHSA-aaaa in root@1.0.0");
        diff.NewAdvisories.ShouldBeEmpty();
        diff.RemovedPackages.ShouldContain("legacy.dep@1.0.0");
        diff.ScoreDelta.ShouldBeGreaterThan(0); // healthier after the upgrade
    }

    private static GraphAssessment Graph(string root, params AssessedNode[] nodes) =>
        new(PackageId.Create(root), nodes, []);

    private static AssessedNode Node(
        string id,
        string version,
        string? license = "MIT",
        bool deprecated = false,
        IReadOnlyList<PackageVulnerability>? vulnerabilities = null)
    {
        var package = PackageId.Create(id);
        var semVer = SemVer.Parse(version);
        var spdx = license is null ? (SpdxLicense?)null : SpdxLicense.Create(license);
        var input = new PackageRiskInput(package, semVer, spdx, spdx, deprecated, false, false, vulnerabilities ?? []);
        return new AssessedNode(package, semVer, input, PackageRiskScorer.Assess(input));
    }

    private static PackageVulnerability Vuln(string id, string version, string advisory) =>
        PackageVulnerability.Create(PackageId.Create(id), SemVer.Parse(version), advisory, RiskLevel.High, "test advisory", "OSV");
}
