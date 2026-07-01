using DepRadar.Application.Ecosystems;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Ecosystems;

public sealed class NpmManifestTests
{
    [Fact]
    public void Parses_runtime_dependencies_and_ignores_dev()
    {
        const string json = """
            {
              "name": "demo",
              "dependencies": { "express": "^4.18.2", "lodash": "~4.17.21", "left-pad": "*" },
              "devDependencies": { "jest": "^29.0.0" }
            }
            """;

        var dependencies = NpmManifest.ParseDependencies(json);

        dependencies.Select(d => d.Name).ShouldBe(["express", "lodash", "left-pad"]);
        dependencies[0].Specifier.ShouldBe("^4.18.2");
    }

    [Fact]
    public void Keeps_non_registry_specifiers_verbatim_for_the_unresolved_report()
    {
        const string json = """{ "dependencies": { "mylib": "git+https://github.com/o/r.git" } }""";

        NpmManifest.ParseDependencies(json).Single().Specifier.ShouldBe("git+https://github.com/o/r.git");
    }

    [Fact]
    public void No_dependencies_object_yields_empty()
    {
        NpmManifest.ParseDependencies("""{ "name": "demo" }""").ShouldBeEmpty();
    }

    [Fact]
    public void Invalid_json_throws_format_exception()
    {
        Should.Throw<FormatException>(() => NpmManifest.ParseDependencies("not json"));
        Should.Throw<FormatException>(() => NpmManifest.ParseDependencies("[1,2]"));
    }
}

public sealed class RequirementsFileTests
{
    [Fact]
    public void Parses_requirements_skipping_comments_options_and_extras()
    {
        const string content = """
            # production deps
            requests==2.19.1
            urllib3>=1.21.1,<1.27  # pinned below 2.x
            flask [async] >= 2.0 ; python_version >= '3.8'
            -r other-requirements.txt
            --hash=sha256:deadbeef
            -e ./local-package

            django \
                >=4.2,<5.0
            """;

        var dependencies = RequirementsFile.Parse(content);

        dependencies.Select(d => d.Name).ShouldBe(["requests", "urllib3", "flask", "django"]);
        dependencies.Single(d => d.Name == "urllib3").Specifier.ShouldBe(">=1.21.1,<1.27");
        // The backslash continuation folds into one logical requirement.
        dependencies.Single(d => d.Name == "django").Specifier.ShouldBe(">=4.2,<5.0");
    }

    [Fact]
    public void Bare_names_carry_an_empty_specifier()
    {
        RequirementsFile.Parse("idna\n").Single().ShouldBe(new ManifestDependency("idna", string.Empty));
    }

    [Fact]
    public void Empty_and_comment_only_content_yields_empty()
    {
        RequirementsFile.Parse("# nothing here\n\n").ShouldBeEmpty();
    }
}
