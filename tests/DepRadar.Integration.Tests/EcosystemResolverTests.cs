using System.Net;
using System.Text;
using DepRadar.Application;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Risk;
using DepRadar.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Drives the npm and PyPI scanners end-to-end against canned registry + OSV HTTP
/// responses — no network, no database. This pins down the transitive BFS resolution,
/// range/PEP 440 specifier matching, node de-duplication and OSV severity mapping
/// deterministically: the ecosystem cores that were previously only verified live.
/// </summary>
public sealed class EcosystemResolverTests
{
    [Fact]
    public async Task Npm_resolves_transitive_graph_dedups_and_maps_a_cve()
    {
        // express -> cookie, body-parser ; body-parser -> cookie (diamond on cookie).
        var registry = new RouteHandler(new Dictionary<string, string>
        {
            ["/express"] = """{"dist-tags":{"latest":"4.18.2"},"versions":{"4.18.2":{"dependencies":{"cookie":"^0.5.0","body-parser":"^1.20.0"},"license":"MIT"}}}""",
            ["/body-parser"] = """{"dist-tags":{"latest":"1.20.1"},"versions":{"1.20.0":{"dependencies":{},"license":"MIT"},"1.20.1":{"dependencies":{"cookie":"^0.5.0"},"license":"MIT"}}}""",
            ["/cookie"] = """{"dist-tags":{"latest":"0.5.0"},"versions":{"0.5.0":{"dependencies":{},"license":"MIT"}}}""",
        });
        var osv = new OsvHandler(vulnerablePackage: "cookie");

        var assessment = await ScanAsync<INpmScanner>(
            ("NpmRegistryClient", registry),
            ("NpmVulnerabilitySource", osv),
            scanner => scanner.ScanAsync("express", null, TestContext.Current.CancellationToken));

        assessment.ShouldNotBeNull();
        // cookie is reached via two paths but appears once.
        assessment.Nodes.Select(n => n.Package.Value).OrderBy(x => x)
            .ShouldBe(["body-parser", "cookie", "express"]);
        // ^1.20.0 over {1.20.0, 1.20.1} resolves to the highest satisfying version.
        assessment.Nodes.Single(n => n.Package.Value == "body-parser").Version.ToString().ShouldBe("1.20.1");
        // express->cookie, express->body-parser, body-parser->cookie
        assessment.Edges.Count.ShouldBe(3);
        // the OSV advisory on cookie was mapped onto its node.
        assessment.Nodes.Single(n => n.Package.Value == "cookie").Input.Vulnerabilities.ShouldNotBeEmpty();
        assessment.Nodes.Single(n => n.Package.Value == "express").Input.Vulnerabilities.ShouldBeEmpty();
    }

    [Fact]
    public async Task PyPi_applies_pep440_specifiers_and_skips_extras()
    {
        // requests 2.19.1 -> urllib3 (>=1.21.1,<1.24), idna (>=2.5,<2.8); PySocks is extra-gated.
        var registry = new RouteHandler(new Dictionary<string, string>
        {
            ["pypi/requests/json"] = """{"info":{"version":"2.31.0","requires_dist":null,"license":"Apache-2.0"},"releases":{"2.19.1":[],"2.31.0":[]}}""",
            ["pypi/requests/2.19.1/json"] = """{"info":{"version":"2.19.1","requires_dist":["urllib3 (>=1.21.1,<1.24)","idna (>=2.5,<2.8)","PySocks (>=1.5.6); extra == 'socks'"],"license":"Apache-2.0"},"releases":{"2.19.1":[],"2.31.0":[]}}""",
            ["pypi/urllib3/json"] = """{"info":{"version":"1.24","requires_dist":null,"license":"MIT"},"releases":{"1.22":[],"1.23":[],"1.24":[]}}""",
            ["pypi/urllib3/1.23/json"] = """{"info":{"version":"1.23","requires_dist":null,"license":"MIT"},"releases":{"1.22":[],"1.23":[],"1.24":[]}}""",
            ["pypi/idna/json"] = """{"info":{"version":"2.7","requires_dist":null,"license":"BSD"},"releases":{"2.7":[]}}""",
            ["pypi/idna/2.7/json"] = """{"info":{"version":"2.7","requires_dist":null,"license":"BSD"},"releases":{"2.7":[]}}""",
        });
        var osv = new OsvHandler(vulnerablePackage: "urllib3");

        var assessment = await ScanAsync<IPyPiScanner>(
            ("PyPiRegistryClient", registry),
            ("PyPiVulnerabilitySource", osv),
            scanner => scanner.ScanAsync("requests", "2.19.1", TestContext.Current.CancellationToken));

        assessment.ShouldNotBeNull();
        // PySocks (extra == 'socks') is not a runtime dependency and must be skipped.
        assessment.Nodes.Select(n => n.Package.Value).OrderBy(x => x)
            .ShouldBe(["idna", "requests", "urllib3"]);
        // "<1.24" excludes 1.24, so the compatible release is 1.23 (normalized to 1.23.0).
        assessment.Nodes.Single(n => n.Package.Value == "urllib3").Version.ToString().ShouldBe("1.23.0");
        assessment.Nodes.Single(n => n.Package.Value == "urllib3").Input.Vulnerabilities.ShouldNotBeEmpty();
    }

    /// <summary>Wires Application + Infrastructure, swaps the two named clients' handlers, runs the scanner.</summary>
    private static async Task<GraphAssessment?> ScanAsync<TScanner>(
        (string Name, HttpMessageHandler Handler) registry,
        (string Name, HttpMessageHandler Handler) vulnerabilities,
        Func<TScanner, Task<GraphAssessment?>> scan)
        where TScanner : notnull
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure();
        services.AddHttpClient(registry.Name).ConfigurePrimaryHttpMessageHandler(() => registry.Handler);
        services.AddHttpClient(vulnerabilities.Name).ConfigurePrimaryHttpMessageHandler(() => vulnerabilities.Handler);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await scan(scope.ServiceProvider.GetRequiredService<TScanner>());
    }

    /// <summary>Serves a canned JSON body for the first route key contained in the request URL; 404 otherwise.</summary>
    private sealed class RouteHandler(IReadOnlyDictionary<string, string> routes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var match = routes.FirstOrDefault(route => url.Contains(route.Key, StringComparison.OrdinalIgnoreCase));
            var status = match.Value is null ? HttpStatusCode.NotFound : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(match.Value ?? string.Empty, Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>OSV stub: returns one HIGH advisory iff the queried package name matches.</summary>
    private sealed class OsvHandler(string vulnerablePackage) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var hit = body.Contains($"\"{vulnerablePackage}\"", StringComparison.Ordinal);
            var json = hit
                ? """{"vulns":[{"id":"GHSA-test-0001","summary":"Test advisory","aliases":["CVE-2024-0001"],"database_specific":{"severity":"HIGH"}}]}"""
                : """{"vulns":[]}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }
}
