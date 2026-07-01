using DepRadar.Application.Ecosystems;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Ecosystems;

public sealed class GoVersionTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]                                   // prefix optional on input
    [InlineData("v2.0.0+incompatible", "2.0.0+incompatible")]        // pre-modules major
    [InlineData("v0.0.0-20190101000000-abcdef123456", "0.0.0-20190101000000-abcdef123456")] // pseudo-version
    public void Parses_go_version_forms(string input, string expected)
    {
        GoVersion.TryParse(input, out var version).ShouldBeTrue();
        version.ToString().ShouldBe(expected);
    }

    [Fact]
    public void Pseudo_versions_rank_below_tagged_releases()
    {
        GoVersion.TryParse("v0.0.0-20190101000000-abcdef123456", out var pseudo).ShouldBeTrue();
        pseudo.IsStable.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    public void Rejects_non_versions(string input)
    {
        GoVersion.TryParse(input, out _).ShouldBeFalse();
    }
}

public sealed class GoModTests
{
    [Fact]
    public void Parses_single_and_block_requires_skipping_indirect()
    {
        const string mod = """
            module example.com/demo

            go 1.22

            require github.com/gin-gonic/gin v1.9.1

            require (
                golang.org/x/text v0.3.7
                github.com/stretchr/testify v1.8.0 // indirect
                gopkg.in/yaml.v3 v3.0.1
            )

            replace example.com/other => ../other
            """;

        GoMod.ParseRequires(mod).ShouldBe(
        [
            new ManifestDependency("github.com/gin-gonic/gin", "v1.9.1"),
            new ManifestDependency("golang.org/x/text", "v0.3.7"),
            new ManifestDependency("gopkg.in/yaml.v3", "v3.0.1"),
        ]);
    }

    [Fact]
    public void Content_without_requires_yields_empty()
    {
        GoMod.ParseRequires("module example.com/demo\n\ngo 1.22\n").ShouldBeEmpty();
    }
}

public sealed class GoSumTests
{
    [Fact]
    public void Folds_gomod_hash_lines_and_dedupes()
    {
        const string sum = """
            golang.org/x/text v0.3.7 h1:olpwvP2KacW1ZWvsR7uQhoyTYvKAupfQrRGBFM352Gk=
            golang.org/x/text v0.3.7/go.mod h1:u+2+/6zg+i71rQMx5EYifcz6MCKuco9NR6JIITiCfzQ=
            github.com/gin-gonic/gin v1.9.1 h1:4idEAncQnU5cB7BeOkPtxjfCSye0AAm1R0RVIqJ+Jmg=
            """;

        GoSum.Parse(sum).ShouldBe(
        [
            new LockedPackage("golang.org/x/text", "v0.3.7"),
            new LockedPackage("github.com/gin-gonic/gin", "v1.9.1"),
        ]);
    }
}
