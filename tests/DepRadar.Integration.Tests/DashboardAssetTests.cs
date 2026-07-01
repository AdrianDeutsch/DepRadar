using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Guards the static dashboard against regression: the assets the API serves from
/// <c>wwwroot</c> must exist and stay wired to each other and to the API. This is a
/// cheap, deterministic smoke test — a full browser E2E (Playwright) is deliberately
/// out of scope for this project's test infrastructure.
/// </summary>
public sealed class DashboardAssetTests
{
    [Fact]
    public void Dashboard_assets_exist_and_are_wired_together()
    {
        var wwwroot = Path.Combine(RepoRoot(), "src", "DepRadar.Api", "wwwroot");

        var indexPath = Path.Combine(wwwroot, "index.html");
        File.Exists(indexPath).ShouldBeTrue("index.html is missing");
        File.Exists(Path.Combine(wwwroot, "app.js")).ShouldBeTrue("app.js is missing");
        File.Exists(Path.Combine(wwwroot, "styles.css")).ShouldBeTrue("styles.css is missing");

        var html = File.ReadAllText(indexPath);
        html.ShouldContain("src=\"app.js\"", customMessage: "index.html no longer loads app.js");
        html.ShouldContain("href=\"styles.css\"", customMessage: "index.html no longer loads styles.css");
        // Core dashboard landmarks the script binds to.
        foreach (var id in new[] { "packageInput", "scanButton", "graph", "riskTable" })
        {
            html.ShouldContain($"id=\"{id}\"", customMessage: $"dashboard landmark #{id} is gone");
        }

        // The script must still talk to the API surface it renders.
        File.ReadAllText(Path.Combine(wwwroot, "app.js"))
            .ShouldContain("/api/", customMessage: "app.js no longer calls the API");
    }

    /// <summary>Walks up from the test output directory to the repo root (the folder holding the .slnx).</summary>
    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DepRadar.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repo root (DepRadar.slnx).");
    }
}
