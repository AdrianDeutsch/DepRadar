using DepRadar.Application;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Chat;
using DepRadar.Application.Messaging;
using DepRadar.Application.Sbom;
using DepRadar.Application.Scans;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// End-to-end SBOM export and graph chat against real Postgres: scan a package, then
/// produce a CycloneDX SBOM and ask natural-language questions about the graph.
/// </summary>
public sealed class SbomChatTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Sbom_and_chat_reflect_the_scanned_graph()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await MigrateAsync(provider);

        var queued = await SendAsync(provider, new RequestScanCommand("SbomRoot"));
        await SendAsync(provider, new RunScanCommand(queued.Id));

        // CycloneDX SBOM contains the components, B's advisory and the dependency graph.
        var sbom = await SendAsync(provider, new GetSbomQuery("SbomRoot"));
        sbom.ShouldNotBeNull();
        sbom!.ShouldContain("\"bomFormat\": \"CycloneDX\"");
        sbom.ShouldContain("pkg:nuget/sbomroot@1.0.0");
        sbom.ShouldContain("GHSA-test-0001");
        sbom.ShouldContain("\"dependsOn\"");

        // Chat answers deterministically (no LLM key) over the same graph.
        var deprecated = await SendAsync(provider, new AskGraphQuestionQuery("SbomRoot", "which packages are deprecated?"));
        deprecated.ShouldNotBeNull();
        deprecated!.LlmUsed.ShouldBeFalse();
        deprecated.Packages.ShouldContain("c"); // C is the deprecated stub node

        var riskiest = await SendAsync(provider, new AskGraphQuestionQuery("SbomRoot", "what is the riskiest package?"));
        riskiest!.Packages.ShouldContain("b"); // B carries the critical advisory
    }

    private static async Task MigrateAsync(IServiceProvider provider)
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
        services.AddScoped<IRepositoryHealthEnricher, StubRepositoryHealthEnricher>();

        return services.BuildServiceProvider();
    }
}
