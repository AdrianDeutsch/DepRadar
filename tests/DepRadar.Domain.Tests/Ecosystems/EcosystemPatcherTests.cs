using DepRadar.Application.Ecosystems;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Ecosystems;

public sealed class NpmManifestPatcherTests
{
    private const string Manifest = """
        {
          "name": "demo",
          "dependencies": {
            "express": "^4.18.0",
            "minimist": "~1.2.0",
            "left-pad": "1.3.0"
          },
          "devDependencies": {
            "express": "^4.0.0"
          }
        }
        """;

    [Fact]
    public void Keeps_the_declared_operator_and_leaves_dev_dependencies_untouched()
    {
        var patch = NpmManifestPatcher.Apply(Manifest, new Dictionary<string, string>
        {
            ["express"] = "4.22.2",
            ["minimist"] = "1.2.8",
            ["left-pad"] = "1.3.1",
        });

        patch.Applied.Count.ShouldBe(3);
        patch.Content.ShouldContain("\"express\": \"^4.22.2\"");   // caret preserved
        patch.Content.ShouldContain("\"minimist\": \"~1.2.8\"");   // tilde preserved
        patch.Content.ShouldContain("\"left-pad\": \"1.3.1\"");    // exact stays exact
        patch.Content.ShouldContain("\"express\": \"^4.0.0\"");    // devDependencies untouched
    }

    [Fact]
    public void Unknown_package_and_missing_dependencies_block_apply_nothing()
    {
        NpmManifestPatcher.Apply(Manifest, new Dictionary<string, string> { ["unknown"] = "1.0.0" })
            .Applied.ShouldBeEmpty();
        NpmManifestPatcher.Apply("""{ "name": "demo" }""", new Dictionary<string, string> { ["express"] = "1.0.0" })
            .Applied.ShouldBeEmpty();
    }
}

public sealed class RequirementsPatcherTests
{
    private const string Requirements = """
        # deps
        requests==2.19.1  # old on purpose
        urllib3>=1.21.1,<1.24
        Django == 4.1.0
        """;

    [Fact]
    public void Bumps_exact_pins_preserving_comments_and_skips_ranges()
    {
        var patch = RequirementsPatcher.Apply(Requirements, new Dictionary<string, string>
        {
            ["requests"] = "2.32.3",
            ["urllib3"] = "2.0.0",   // range line: no unambiguous pin to rewrite
            ["django"] = "4.2.0",    // canonical-name match against "Django"
        });

        patch.Applied.Select(b => b.Package).ShouldBe(["requests", "django"]);
        patch.Content.ShouldContain("requests==2.32.3  # old on purpose");
        patch.Content.ShouldContain("urllib3>=1.21.1,<1.24"); // untouched
        patch.Content.ShouldContain("Django == 4.2.0");
    }

    [Fact]
    public void Identical_target_version_applies_nothing()
    {
        RequirementsPatcher.Apply("requests==2.19.1", new Dictionary<string, string> { ["requests"] = "2.19.1" })
            .Applied.ShouldBeEmpty();
    }
}
