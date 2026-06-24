using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.ValueObjects;

public sealed class SemVerTests
{
    [Fact]
    public void Parse_extracts_all_components()
    {
        var version = SemVer.Parse("2.5.1-rc.2+build.7");

        version.Major.ShouldBe(2);
        version.Minor.ShouldBe(5);
        version.Patch.ShouldBe(1);
        version.Prerelease.ShouldBe("rc.2");
        version.BuildMetadata.ShouldBe("build.7");
        version.IsStable.ShouldBeFalse();
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("1.2")]
    [InlineData("1.2.x")]
    [InlineData("")]
    public void TryParse_rejects_invalid_input(string value)
    {
        SemVer.TryParse(value, out _).ShouldBeFalse();
    }

    [Fact]
    public void Stable_version_outranks_its_prerelease()
    {
        (SemVer.Parse("1.0.0") > SemVer.Parse("1.0.0-alpha")).ShouldBeTrue();
    }

    [Fact]
    public void Build_metadata_is_ignored_for_precedence()
    {
        SemVer.Parse("1.0.0+build.1").ShouldBe(SemVer.Parse("1.0.0+build.2"));
    }

    [Fact]
    public void Prerelease_precedence_follows_the_specification()
    {
        // The canonical ordering example from semver.org, section 11.
        var ascending = new[]
        {
            "1.0.0-alpha",
            "1.0.0-alpha.1",
            "1.0.0-alpha.beta",
            "1.0.0-beta",
            "1.0.0-beta.2",
            "1.0.0-beta.11",
            "1.0.0-rc.1",
            "1.0.0",
        };

        for (var i = 0; i < ascending.Length - 1; i++)
        {
            var lower = SemVer.Parse(ascending[i]);
            var higher = SemVer.Parse(ascending[i + 1]);
            (lower < higher).ShouldBeTrue($"{lower} should rank below {higher}");
        }
    }

    [Fact]
    public void Supports_the_nuget_fourth_component()
    {
        var version = SemVer.Parse("1.2.3.4");

        version.Revision.ShouldBe(4);
        version.ToString().ShouldBe("1.2.3.4");
        (SemVer.Parse("1.2.3") < SemVer.Parse("1.2.3.1")).ShouldBeTrue();
        // A zero revision is normalized away, matching NuGet.
        SemVer.Parse("1.2.3.0").ToString().ShouldBe("1.2.3");
    }

    [Fact]
    public void Sorting_orders_by_release_then_prerelease()
    {
        var versions = new[] { "2.0.0", "1.10.0", "1.9.0", "2.0.0-rc.1", "1.9.0-beta" }
            .Select(SemVer.Parse)
            .Order()
            .Select(v => v.ToString())
            .ToArray();

        versions.ShouldBe(["1.9.0-beta", "1.9.0", "1.10.0", "2.0.0-rc.1", "2.0.0"]);
    }
}
