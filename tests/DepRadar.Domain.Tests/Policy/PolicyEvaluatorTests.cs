using System.Collections.Frozen;
using DepRadar.Application.Policy;
using DepRadar.Application.Risk;
using DepRadar.Domain.Packages;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Policy;

public sealed class PolicyEvaluatorTests
{
    [Fact]
    public void Passes_a_healthy_graph_under_the_default_policy()
    {
        var graph = Graph(Node("safe.pkg", "1.0.0"));

        var outcome = PolicyEvaluator.Evaluate(graph, RiskPolicy.Default);

        outcome.Passed.ShouldBeTrue();
        outcome.Violations.ShouldBeEmpty();
    }

    [Fact]
    public void Fails_when_a_package_meets_the_fail_on_level()
    {
        var graph = Graph(
            Node("bad.pkg", "1.0.0", deprecated: true, license: null), // High: deprecated + unknown license
            Node("safe.pkg", "1.0.0"));

        var outcome = PolicyEvaluator.Evaluate(graph, new RiskPolicy(RiskLevel.High, AllowDeprecated: true, FrozenSet<LicenseCategory>.Empty));

        outcome.Passed.ShouldBeFalse();
        outcome.Violations.ShouldContain(v => v.Package == "bad.pkg@1.0.0" && v.Reason.Contains("threshold", StringComparison.Ordinal));
    }

    [Fact]
    public void Fails_on_deprecated_packages_when_disallowed()
    {
        var graph = Graph(Node("legacy.pkg", "1.0.0", deprecated: true));

        var outcome = PolicyEvaluator.Evaluate(graph, new RiskPolicy(RiskLevel.Critical, AllowDeprecated: false, FrozenSet<LicenseCategory>.Empty));

        outcome.Passed.ShouldBeFalse();
        outcome.Violations.ShouldContain(v => v.Reason.Contains("deprecated", StringComparison.Ordinal));
    }

    [Fact]
    public void Suppresses_violations_for_ignored_packages()
    {
        var graph = Graph(Node("bad.pkg", "1.0.0", deprecated: true, license: null)); // High
        var policy = new RiskPolicy(RiskLevel.High, AllowDeprecated: false, FrozenSet<LicenseCategory>.Empty)
        {
            IgnoredPackages = new[] { "bad.pkg" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
        };

        var outcome = PolicyEvaluator.Evaluate(graph, policy);

        outcome.Passed.ShouldBeTrue(); // accepted risk — suppressed from the gate
        outcome.Violations.ShouldBeEmpty();
    }

    [Fact]
    public void Fails_on_forbidden_license_categories()
    {
        var graph = Graph(Node("gpl.pkg", "1.0.0", license: "GPL-3.0-only"));
        var forbidden = new[] { LicenseCategory.Copyleft }.ToFrozenSet();

        var outcome = PolicyEvaluator.Evaluate(graph, new RiskPolicy(RiskLevel.Critical, AllowDeprecated: true, forbidden));

        outcome.Passed.ShouldBeFalse();
        outcome.Violations.ShouldContain(v => v.Reason.Contains("forbidden", StringComparison.Ordinal));
    }

    private static GraphAssessment Graph(params AssessedNode[] nodes) =>
        new(PackageId.Create("root"), nodes, []);

    private static AssessedNode Node(string id, string version, string? license = "MIT", bool deprecated = false)
    {
        var package = PackageId.Create(id);
        var semVer = SemVer.Parse(version);
        var spdx = license is null ? (SpdxLicense?)null : SpdxLicense.Create(license);
        var input = new PackageRiskInput(package, semVer, spdx, spdx, deprecated, false, false, []);
        return new AssessedNode(package, semVer, input, PackageRiskScorer.Assess(input));
    }
}
