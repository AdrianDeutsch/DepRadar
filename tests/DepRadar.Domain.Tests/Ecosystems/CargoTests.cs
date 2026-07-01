using DepRadar.Application.Ecosystems;
using DepRadar.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Ecosystems;

public sealed class CargoReqTests
{
    [Theory]
    // bare = caret (Cargo's default) — the key difference from npm
    [InlineData("1.2.3", "1.9.0", true)]
    [InlineData("1.2.3", "2.0.0", false)]
    [InlineData("1.2", "1.9.0", true)]
    [InlineData("1", "1.9.0", true)]
    // 0.x caret rules
    [InlineData("0.2.3", "0.2.9", true)]
    [InlineData("0.2.3", "0.3.0", false)]
    // explicit operators
    [InlineData("=1.2.3", "1.2.3", true)]
    [InlineData("=1.2.3", "1.2.4", false)]
    [InlineData("~1.2.3", "1.2.9", true)]
    [InlineData("~1.2.3", "1.3.0", false)]
    [InlineData("*", "3.1.4", true)]
    // comma-AND
    [InlineData(">=1.2, <1.5", "1.4.0", true)]
    [InlineData(">=1.2, <1.5", "1.5.0", false)]
    public void Matches_cargo_requirements(string req, string version, bool expected)
    {
        CargoReq.Satisfies(SemVer.Parse(version), req).ShouldBe(expected);
    }

    [Fact]
    public void BestMatch_picks_the_highest_satisfying_stable_version()
    {
        var candidates = new[] { "1.2.0", "1.4.9", "2.0.0" }.Select(SemVer.Parse);

        CargoReq.BestMatch("1.2", candidates)!.ToString().ShouldBe("1.4.9"); // ^1.2
    }
}

public sealed class CargoManifestTests
{
    [Fact]
    public void Parses_dependency_forms_and_skips_dev_path_and_workspace()
    {
        const string toml = """
            [package]
            name = "demo"
            version = "0.1.0"

            [dependencies]
            serde = "1.0"                                  # bare requirement
            tokio = { version = "1.35", features = ["rt"] } # inline table
            local-lib = { path = "../local" }              # no registry version
            shared = { workspace = true }

            [dependencies.regex]
            version = "1.5"

            [dev-dependencies]
            criterion = "0.5"
            """;

        var dependencies = CargoManifest.ParseDependencies(toml);

        dependencies.ShouldBe(
        [
            new ManifestDependency("serde", "1.0"),
            new ManifestDependency("tokio", "1.35"),
            new ManifestDependency("regex", "1.5"),
        ]);
    }

    [Fact]
    public void Content_without_dependencies_yields_empty()
    {
        CargoManifest.ParseDependencies("[package]\nname = \"demo\"\n").ShouldBeEmpty();
    }
}

public sealed class CargoLockfileTests
{
    [Fact]
    public void Parses_package_blocks_lowercasing_names()
    {
        const string lok = """
            version = 4

            [[package]]
            name = "Serde"
            version = "1.0.190"
            source = "registry+https://github.com/rust-lang/crates.io-index"

            [[package]]
            name = "demo"
            version = "0.1.0"
            """;

        CargoLockfile.Parse(lok).ShouldBe(
        [
            new LockedPackage("serde", "1.0.190"),
            new LockedPackage("demo", "0.1.0"),
        ]);
    }
}
