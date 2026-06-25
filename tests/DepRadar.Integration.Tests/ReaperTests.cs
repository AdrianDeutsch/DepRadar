using DepRadar.Application;
using DepRadar.Application.Abstractions;
using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;
using DepRadar.Infrastructure;
using DepRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace DepRadar.Integration.Tests;

/// <summary>
/// Verifies the stale-scan resilience path against real Postgres: a scan stuck in
/// <c>Running</c> is found and requeued so it can be retried.
/// </summary>
public sealed class ReaperTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task A_stuck_running_scan_is_requeued()
    {
        await using var provider = BuildProvider(fixture.ConnectionString);
        await MigrateAsync(provider);

        var token = TestContext.Current.CancellationToken;
        ScanId scanId;

        // Arrange: a scan abandoned in Running state.
        await using (var scope = provider.CreateAsyncScope())
        {
            var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            var scans = scope.ServiceProvider.GetRequiredService<IScanRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var scan = Scan.Create(PackageId.Create("StuckRoot"), time.GetUtcNow());
            scan.Start(time.GetUtcNow());
            scanId = scan.Id;
            await scans.AddAsync(scan, token);
            await unitOfWork.SaveChangesAsync(token);
        }

        // Act: reap anything started before "now + 1 minute" (i.e. our just-started scan).
        await using (var scope = provider.CreateAsyncScope())
        {
            var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            var scans = scope.ServiceProvider.GetRequiredService<IScanRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var stale = await scans.GetStaleRunningAsync(time.GetUtcNow().AddMinutes(1), 10, token);
            stale.ShouldContain(s => s.Id == scanId);
            foreach (var scan in stale)
            {
                scan.Requeue();
            }

            await unitOfWork.SaveChangesAsync(token);
        }

        // Assert: it is queued again.
        await using var verify = provider.CreateAsyncScope();
        var repository = verify.ServiceProvider.GetRequiredService<IScanRepository>();
        var reloaded = await repository.GetAsync(scanId, token);
        reloaded.ShouldNotBeNull();
        reloaded!.Status.ShouldBe(ScanStatus.Queued);
        reloaded.StartedAt.ShouldBeNull();
    }

    private static async Task MigrateAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DepRadarDbContext>();
        await context.Database.MigrateAsync();
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
