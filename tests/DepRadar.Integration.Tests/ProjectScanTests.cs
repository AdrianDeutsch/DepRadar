using DepRadar.Application;
using DepRadar.Application.Messaging;
using DepRadar.Application.Projects;
using DepRadar.Domain.Packages;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Verifies a project file is parsed and a scan is queued per direct dependency
/// (no worker needed — the scans simply land in the queue).
/// </summary>
public sealed class ProjectScanTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Queues_one_scan_per_direct_package_reference()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await MigrateAsync(provider);

        const string csproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="ProjScanA" Version="1.0.0" />
                <PackageReference Include="ProjScanB" Version="2.0.0" />
              </ItemGroup>
            </Project>
            """;

        var result = await SendAsync(provider, new RequestProjectScanCommand(csproj));

        result.PackageCount.ShouldBe(2);
        result.Packages.Select(p => p.PackageId).ShouldBe(["ProjScanA", "ProjScanB"], ignoreOrder: true);

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DepRadarDbContext>();
        (await context.Scans.CountAsync(s => s.Status == ScanStatus.Queued)).ShouldBeGreaterThanOrEqualTo(2);
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
        return services.BuildServiceProvider();
    }
}
