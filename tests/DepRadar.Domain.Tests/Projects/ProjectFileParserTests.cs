using DepRadar.Application.Projects;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Projects;

public sealed class ProjectFileParserTests
{
    [Fact]
    public void Parses_package_references_from_a_csproj()
    {
        const string csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Serilog" Version="4.0.0" />
                <PackageReference Include="Newtonsoft.Json" />
                <PackageReference Include="Serilog" />
              </ItemGroup>
            </Project>
            """;

        var packages = ProjectFileParser.ParseDirectPackages(csproj);

        packages.ShouldBe(["Serilog", "Newtonsoft.Json"], ignoreOrder: true);
    }

    [Fact]
    public void Parses_only_direct_dependencies_from_a_packages_lock()
    {
        const string lockJson = """
            {
              "version": 1,
              "dependencies": {
                "net10.0": {
                  "Serilog": { "type": "Direct", "resolved": "4.0.0" },
                  "Serilog.Sinks.Console": { "type": "Transitive", "resolved": "6.0.0" }
                }
              }
            }
            """;

        var packages = ProjectFileParser.ParseDirectPackages(lockJson);

        packages.ShouldBe(["Serilog"]);
    }

    [Fact]
    public void Rejects_unrecognized_content()
    {
        Should.Throw<FormatException>(() => ProjectFileParser.ParseDirectPackages("just some text"));
    }
}
