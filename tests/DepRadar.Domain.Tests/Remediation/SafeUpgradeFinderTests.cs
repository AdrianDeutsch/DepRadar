using DepRadar.Application.Remediation;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Remediation;

public sealed class SafeUpgradeFinderTests
{
    [Fact]
    public void Candidates_are_stable_versions_above_current_ascending()
    {
        var candidates = SafeUpgradeFinder.Candidates(
            ["1.0.0", "2.0.0", "1.2.0", "1.5.0-rc.1", "0.9.0"],
            SemVer.Parse("1.0.0"));

        candidates.Select(version => version.ToString()).ShouldBe(["1.2.0", "2.0.0"]); // prerelease + lower dropped
    }

    [Fact]
    public void Candidates_are_capped()
    {
        var versions = Enumerable.Range(2, 50).Select(minor => $"1.{minor}.0").ToList();

        SafeUpgradeFinder.Candidates(versions, SemVer.Parse("1.0.0")).Count.ShouldBe(SafeUpgradeFinder.MaxCandidates);
    }
}
