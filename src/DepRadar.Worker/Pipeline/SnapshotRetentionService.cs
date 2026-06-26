using DepRadar.Application.Abstractions;

namespace DepRadar.Worker.Pipeline;

/// <summary>
/// Retention: periodically prunes each root's drift history to the most recent N
/// snapshots, keeping the table bounded off the scan hot path. Runs on a schedule
/// rather than inline so a scan never pays for cleanup.
/// </summary>
internal sealed class SnapshotRetentionService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SnapshotRetentionService> logger)
    : BackgroundService
{
    private const int DefaultMaxSnapshotsPerRoot = 50;
    private const double DefaultIntervalHours = 6;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hours = configuration.GetValue<double?>("Retention:IntervalHours") ?? DefaultIntervalHours;
        if (hours <= 0)
        {
            return; // retention explicitly disabled
        }

        var keep = Math.Max(2, configuration.GetValue<int?>("Retention:MaxSnapshotsPerRoot") ?? DefaultMaxSnapshotsPerRoot);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(hours));
        while (await WaitAsync(timer, stoppingToken))
        {
            await PruneAllAsync(keep, stoppingToken);
        }
    }

    private async Task PruneAllAsync(int keep, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var snapshots = scope.ServiceProvider.GetRequiredService<IScanSnapshotRepository>();

            var roots = await snapshots.GetTrackedRootsAsync(cancellationToken);
            var removed = 0;
            foreach (var root in roots)
            {
                removed += await snapshots.PruneAsync(root, keep, cancellationToken);
            }

            if (removed > 0)
            {
                logger.LogInformation("Retention pruned {Removed} old snapshot(s) across {Roots} root(s).", removed, roots.Count);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
#pragma warning disable CA1031 // A transient failure must not kill the retention loop.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            logger.LogError(exception, "Snapshot retention failed.");
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
