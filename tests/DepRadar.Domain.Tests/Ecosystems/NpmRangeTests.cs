using DepRadar.Application.Ecosystems;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Ecosystems;

public sealed class NpmRangeTests
{
    [Theory]
    // caret: compatible-within-major (or -minor / -patch for 0.x)
    [InlineData("^1.2.3", "1.2.3", true)]
    [InlineData("^1.2.3", "1.9.0", true)]
    [InlineData("^1.2.3", "2.0.0", false)]
    [InlineData("^1.2.3", "1.2.2", false)]
    [InlineData("^0.2.3", "0.2.9", true)]
    [InlineData("^0.2.3", "0.3.0", false)]
    // tilde: patch-level within minor
    [InlineData("~1.2.3", "1.2.9", true)]
    [InlineData("~1.2.3", "1.3.0", false)]
    // x-ranges
    [InlineData("1.2.x", "1.2.7", true)]
    [InlineData("1.2.x", "1.3.0", false)]
    [InlineData("1.x", "1.9.9", true)]
    [InlineData("1.x", "2.0.0", false)]
    [InlineData("*", "3.1.4", true)]
    // comparator sets (AND)
    [InlineData(">=1.0.0 <2.0.0", "1.5.0", true)]
    [InlineData(">=1.0.0 <2.0.0", "2.0.0", false)]
    // hyphen ranges
    [InlineData("1.2.3 - 1.5.0", "1.5.0", true)]
    [InlineData("1.2.3 - 1.5.0", "1.5.1", false)]
    [InlineData("1.2.0 - 1.5", "1.5.9", true)]
    [InlineData("1.2.0 - 1.5", "1.6.0", false)]
    // unions and exact
    [InlineData("1.0.0 || 2.0.0", "2.0.0", true)]
    [InlineData("1.0.0 || 2.0.0", "1.5.0", false)]
    [InlineData("1.2.3", "1.2.3", true)]
    [InlineData("1.2.3", "1.2.4", false)]
    // caret on 0.0.x pins the patch (a common real-world footgun)
    [InlineData("^0.0.3", "0.0.3", true)]
    [InlineData("^0.0.3", "0.0.4", false)]
    // unions of full comparator sets, not just exact versions
    [InlineData("^1.0.0 || ^2.0.0", "2.5.0", true)]
    [InlineData("^1.0.0 || ^2.0.0", "3.0.0", false)]
    [InlineData(">=1.2.0 <1.3.0 || >=2.0.0", "2.9.9", true)]
    [InlineData(">=1.2.0 <1.3.0 || >=2.0.0", "1.5.0", false)]
    // tilde on a minor-only range, and a bare partial as an implicit range
    [InlineData("~0.2", "0.2.9", true)]
    [InlineData("~0.2", "0.3.0", false)]
    [InlineData("1.2", "1.2.9", true)]
    [InlineData("1.2", "1.3.0", false)]
    public void Satisfies_matches_npm_semantics(string range, string version, bool expected)
    {
        NpmRange.Satisfies(SemVer.Parse(version), range).ShouldBe(expected);
    }

    [Fact]
    public void BestMatch_picks_the_highest_satisfying_version()
    {
        var candidates = new[] { "1.1.0", "1.2.0", "1.5.3", "2.0.0" }.Select(SemVer.Parse);

        NpmRange.BestMatch("^1.2.0", candidates)!.ToString().ShouldBe("1.5.3");
    }

    [Fact]
    public void BestMatch_prefers_a_stable_version_over_a_higher_prerelease()
    {
        var candidates = new[] { "1.5.3", "1.6.0-rc.1" }.Select(SemVer.Parse);

        NpmRange.BestMatch("^1.2.0", candidates)!.ToString().ShouldBe("1.5.3");
    }
}
