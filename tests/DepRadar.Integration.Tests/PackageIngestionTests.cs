using DepRadar.Application;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Application.Packages;
using DepRadar.Domain.ValueObjects;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// End-to-end persistence test for the Slice 1 vertical: a package flows through the
/// real mediator, handler, repository and EF Core into a real Postgres database.
/// </summary>
public sealed class PackageIngestionTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Ingesting_a_package_persists_it_with_its_versions()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await EnsureSchemaAsync(provider);

        var result = await SendAsync(provider, new IngestPackageCommand("Newtonsoft.Json"));

        result.Id.ShouldBe("Newtonsoft.Json");
        result.License.ShouldBe("MIT");
        result.SourceRepositoryUrl.ShouldBe("https://github.com/JamesNK/Newtonsoft.Json");
        result.LatestStableVersion.ShouldBe("13.0.3");
        result.Versions.Count.ShouldBe(2);
        // Versions come back highest-precedence first.
        result.Versions[0].Version.ShouldBe("13.0.3");
    }

    [Fact]
    public async Task Ingesting_the_same_package_twice_is_idempotent()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await EnsureSchemaAsync(provider);

        await SendAsync(provider, new IngestPackageCommand("Serilog"));
        await SendAsync(provider, new IngestPackageCommand("Serilog"));

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DepRadarDbContext>();

        var packageId = PackageId.Create("Serilog");
        (await context.Packages.CountAsync(p => p.Id == packageId)).ShouldBe(1);
        (await context.PackageVersions.CountAsync(v => v.PackageId == packageId)).ShouldBe(2);
    }

    private static async Task EnsureSchemaAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DepRadarDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    private static async Task<PackageDto> SendAsync(IServiceProvider provider, IngestPackageCommand command)
    {
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command);
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DepRadarDbContext>(options => options.UseNpgsql(connectionString));
        services.AddApplication();
        services.AddInfrastructure();

        // Replace the real deps.dev source with a deterministic stub so the test
        // depends only on Postgres, never on an external API.
        services.AddSingleton<IPackageMetadataSource, StubMetadataSource>();

        return services.BuildServiceProvider();
    }
}
