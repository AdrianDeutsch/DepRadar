using DepRadar.Application;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Graphs;
using DepRadar.Application.Messaging;
using DepRadar.Application.Scans;
using DepRadar.Domain.ValueObjects;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Exercises the Slice 2 vertical against real PostgreSQL: request a scan, run it,
/// and read back the transitive closure via the recursive CTE. Also proves the graph
/// upsert is idempotent across separate scans of the same root.
/// </summary>
public sealed class ScanGraphTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Running_a_scan_persists_the_transitive_graph()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await EnsureSchemaAsync(provider);

        var queued = await SendAsync(provider, new RequestScanCommand("DiamondRoot"));
        queued.Status.ShouldBe("Queued");

        var completed = await SendAsync(provider, new RunScanCommand(queued.Id));

        completed.Status.ShouldBe("Completed");
        completed.PackagesDiscovered.ShouldBe(3);
        completed.EdgesWritten.ShouldBe(3);

        var graph = await SendAsync(provider, new GetPackageGraphQuery("DiamondRoot"));

        graph.ShouldNotBeNull();
        graph!.Root.ShouldBe("DiamondRoot");
        graph.Nodes.Count.ShouldBe(3);
        graph.Nodes.ShouldContain(node => node.IsRoot && node.PackageId == "diamondroot");
        graph.Edges.Count.ShouldBe(3);
        // The transitive (non-direct) edge B -> C sits at depth 2.
        graph.Edges.ShouldContain(edge => edge.FromId == "b" && edge.ToId == "c" && !edge.IsDirect && edge.Depth == 2);
    }

    [Fact]
    public async Task Re_scanning_the_same_root_does_not_duplicate_edges()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await EnsureSchemaAsync(provider);

        var first = await SendAsync(provider, new RequestScanCommand("IdempotentRoot"));
        await SendAsync(provider, new RunScanCommand(first.Id));

        var second = await SendAsync(provider, new RequestScanCommand("IdempotentRoot"));
        await SendAsync(provider, new RunScanCommand(second.Id));

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DepRadarDbContext>();

        // Root-scoped assertions: the shared container means B/C may exist from
        // other tests, but the root's own direct edges and version must not duplicate.
        var rootId = PackageId.Create("IdempotentRoot");
        (await context.DependencyEdges.CountAsync(e => e.DependentId == rootId)).ShouldBe(2);
        (await context.PackageVersions.CountAsync(v => v.PackageId == rootId)).ShouldBe(1);
    }

    private static async Task EnsureSchemaAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DepRadarDbContext>();
        await context.Database.EnsureCreatedAsync();
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
        services.AddDbContext<DepRadarDbContext>(options => options.UseNpgsql(connectionString));
        services.AddApplication();
        services.AddInfrastructure();

        // Deterministic graph + advisories instead of the real NuGet/OSV clients.
        services.AddScoped<IDependencyGraphResolver, StubGraphResolver>();
        services.AddScoped<IVulnerabilitySource, StubVulnerabilitySource>();

        return services.BuildServiceProvider();
    }
}
