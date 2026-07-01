using System.Net;
using System.Text;
using DepRadar.Application.Abstractions;
using DepRadar.Domain.ValueObjects;
using DepRadar.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Verifies external responses are cached (HybridCache): a repeated identical query
/// is served from the cache and never re-hits the network — no DB needed.
/// </summary>
public sealed class CachingTests
{
    [Fact]
    public async Task Identical_vulnerability_queries_hit_the_network_only_once()
    {
        var handler = new CountingHandler("""{"vulns":[]}""");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure();
        // Replace the OSV client's primary handler with a counting one (same named client).
        services.AddHttpClient("OsvVulnerabilitySource").ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<IVulnerabilitySource>();

        var package = PackageId.Create("Some.Package");
        var version = SemVer.Parse("1.0.0");

        await source.GetAsync(package, version, TestContext.Current.CancellationToken);
        await source.GetAsync(package, version, TestContext.Current.CancellationToken);

        handler.CallCount.ShouldBe(1);
    }

    private sealed class CountingHandler(string responseJson) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
        }
    }
}
