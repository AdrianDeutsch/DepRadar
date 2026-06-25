using DepRadar.Application;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Application.Scans;
using DepRadar.Application.Upgrades;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// End-to-end RAG + upgrade advice against real pgvector: a scan embeds synthetic
/// changelog chunks, and the upgrade query retrieves them by cosine similarity, builds
/// a shielded prompt and produces a deterministic recommendation (keyless path).
/// </summary>
public sealed class RagTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Scan_indexes_changelog_and_upgrade_advice_uses_rag()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await EnsureSchemaAsync(provider);

        var queued = await SendAsync(provider, new RequestScanCommand("RagRoot"));
        await SendAsync(provider, new RunScanCommand(queued.Id));

        // The scan embedded at least the root's changelog chunk into pgvector.
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<DepRadarDbContext>();
            (await context.ChangelogChunks.CountAsync()).ShouldBeGreaterThan(0);
        }

        var advice = await SendAsync(provider, new GetUpgradeAdviceQuery("RagRoot", null, null));

        advice.ShouldNotBeNull();
        advice!.LlmUsed.ShouldBeFalse(); // no API key -> templated narrative
        advice.Recommendation.ShouldBe("Caution"); // root has a High license-shift finding
        // RAG retrieved the root's synthetic changelog chunk into the shielded prompt.
        advice.Prompt.ShouldContain("RagRoot 1.0.0");
        advice.Prompt.ShouldContain("UNTRUSTED");
        advice.KeyPoints.ShouldContain(point => point.Contains("LicenseShift", StringComparison.Ordinal));
    }

    private static async Task EnsureSchemaAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DepRadarDbContext>();
        await context.Database.MigrateAsync();
    }

    private static async Task<TResponse> SendAsync<TResponse>(IServiceProvider provider, IRequest<TResponse> request)
    {
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request);
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDepRadarDbContext(connectionString);
        services.AddApplication();
        services.AddInfrastructure();
        services.AddScoped<IDependencyGraphResolver, StubGraphResolver>();
        services.AddScoped<IVulnerabilitySource, StubVulnerabilitySource>();

        return services.BuildServiceProvider();
    }
}
