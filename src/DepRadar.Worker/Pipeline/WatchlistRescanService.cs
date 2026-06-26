using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Application.Scans;

namespace DepRadar.Worker.Pipeline;

/// <summary>
/// Autonomous monitoring: periodically re-queues a scan for every package that has
/// ever been scanned (the implicit watchlist). Combined with drift alerts this turns
/// DepRadar into a continuous monitor — you get pinged when a dependency newly becomes
/// vulnerable, deprecated or archived, without lifting a finger.
/// </summary>
/// <remarks>
/// Disabled by default: it only runs when <c>Watch:IntervalHours</c> is set above zero,
/// so normal/test runs never re-scan in the background.
/// </remarks>
internal sealed class WatchlistRescanService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WatchlistRescanService> logger)
    : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hours = configuration.GetValue<double>("Watch:IntervalHours");
        if (hours <= 0)
        {
            return; // monitoring is opt-in
        }

        logger.LogInformation("Watchlist monitoring enabled: re-scanning tracked packages every {Hours}h.", hours);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(hours));
        while (await WaitAsync(timer, stoppingToken))
        {
            await RescanAsync(stoppingToken);
        }
    }

    private async Task RescanAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var snapshots = scope.ServiceProvider.GetRequiredService<IScanSnapshotRepository>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var roots = await snapshots.GetTrackedRootsAsync(cancellationToken);
            foreach (var root in roots)
            {
                await sender.Send(new RequestScanCommand(root.Value), cancellationToken);
            }

            if (roots.Count > 0)
            {
                logger.LogInformation("Watchlist re-queued {Count} package(s) for re-scan.", roots.Count);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
#pragma warning disable CA1031 // A transient failure must not kill the monitoring loop.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            logger.LogError(exception, "Watchlist re-scan failed.");
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
