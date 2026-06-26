using DepRadar.Application.Projects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Projects;

public sealed class ManifestPatcherTests
{
    [Fact]
    public void Bumps_a_package_reference_version_and_leaves_the_rest_untouched()
    {
        const string Csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="10.0.2" />
                <PackageReference Include="Serilog" Version="4.0.0" />
              </ItemGroup>
            </Project>
            """;

        var patch = ManifestPatcher.Apply(Csproj, new Dictionary<string, string> { ["Newtonsoft.Json"] = "13.0.1" });

        patch.Content.ShouldContain("Include=\"Newtonsoft.Json\" Version=\"13.0.1\"");
        patch.Content.ShouldContain("Include=\"Serilog\" Version=\"4.0.0\""); // untouched
        var bump = patch.Applied.ShouldHaveSingleItem();
        bump.Package.ShouldBe("Newtonsoft.Json");
        bump.FromVersion.ShouldBe("10.0.2");
        bump.ToVersion.ShouldBe("13.0.1");
    }

    [Fact]
    public void Patches_central_package_management_package_version_too()
    {
        const string Props = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="10.0.2" />
              </ItemGroup>
            </Project>
            """;

        var patch = ManifestPatcher.Apply(Props, new Dictionary<string, string> { ["Newtonsoft.Json"] = "13.0.1" });

        patch.Content.ShouldContain("Version=\"13.0.1\"");
        patch.Applied.ShouldHaveSingleItem().ToVersion.ShouldBe("13.0.1");
    }

    [Fact]
    public void Reports_no_change_when_the_package_is_absent()
    {
        const string Csproj = """<Project><ItemGroup><PackageReference Include="Serilog" Version="4.0.0" /></ItemGroup></Project>""";

        var patch = ManifestPatcher.Apply(Csproj, new Dictionary<string, string> { ["Newtonsoft.Json"] = "13.0.1" });

        patch.Applied.ShouldBeEmpty();
        patch.Content.ShouldBe(Csproj);
    }
}
