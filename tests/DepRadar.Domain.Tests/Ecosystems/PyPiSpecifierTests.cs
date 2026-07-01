using DepRadar.Application.Ecosystems;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Ecosystems;

public sealed class PyPiSpecifierTests
{
    [Theory]
    // comparators + comma-AND
    [InlineData(">=1.0,<2.0", "1.5.0", true)]
    [InlineData(">=1.0,<2.0", "2.0.0", false)]
    [InlineData(">=1.0,!=1.2.0,<2.0", "1.2.0", false)]
    [InlineData(">=1.0,!=1.2.0,<2.0", "1.5.0", true)]
    // compatible release (~=)
    [InlineData("~=1.4.2", "1.4.9", true)]
    [InlineData("~=1.4.2", "1.5.0", false)]
    [InlineData("~=1.4.2", "1.4.1", false)]
    [InlineData("~=1.4", "1.9.0", true)]
    [InlineData("~=1.4", "2.0.0", false)]
    // wildcard equality
    [InlineData("==1.4.*", "1.4.7", true)]
    [InlineData("==1.4.*", "1.5.0", false)]
    [InlineData("==2.*", "2.9.9", true)]
    [InlineData("==2.*", "3.0.0", false)]
    // exact / not-equal / arbitrary equality
    [InlineData("==2.0.0", "2.0.0", true)]
    [InlineData("==2.0.0", "2.0.1", false)]
    [InlineData("!=1.5.0", "1.5.0", false)]
    [InlineData("===1.2.3", "1.2.3", true)]
    // two-part compatible release: ~=2.2 := >=2.2, <3.0
    [InlineData("~=2.2", "2.9.0", true)]
    [InlineData("~=2.2", "3.0.0", false)]
    // whitespace around comma-separated clauses must be tolerated
    [InlineData(">= 1.0, < 2.0", "1.5.0", true)]
    [InlineData(">= 1.0, < 2.0", "2.0.0", false)]
    // a bare version is an exact match
    [InlineData("2.0.0", "2.0.0", true)]
    [InlineData("2.0.0", "2.0.1", false)]
    public void Matches_pep440_specifiers(string specifier, string version, bool expected)
    {
        PyPiSpecifier.Matches(SemVer.Parse(version), specifier).ShouldBe(expected);
    }

    [Fact]
    public void BestMatch_picks_the_highest_satisfying_stable_version()
    {
        var candidates = new[] { "1.9.0", "2.1.0", "2.9.0", "3.0.0" }.Select(SemVer.Parse);

        PyPiSpecifier.BestMatch(">=2.0,<3.0", candidates)!.ToString().ShouldBe("2.9.0");
    }
}
