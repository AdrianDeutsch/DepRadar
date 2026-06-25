using DepRadar.Application.Abstractions;

namespace DepRadar.Worker.Pipeline;

/// <summary>
/// Resilience: requeues scans stuck in <c>Running</c> (e.g. abandoned when a worker
/// crashed mid-scan) so they are retried rather than lost. The handler's idempotent
/// upserts make re-running safe.
/// </summary>
internal sealed class StaleScanReaper(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<StaleScanReaper> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);
    private const int BatchSize = 50;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await WaitAsync(timer, stoppingToken))
        {
            await ReapAsync(stoppingToken);
        }
    }

    private async Task ReapAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var scans = scope.ServiceProvider.GetRequiredService<IScanRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var threshold = timeProvider.GetUtcNow() - StaleAfter;
            var stale = await scans.GetStaleRunningAsync(threshold, BatchSize, cancellationToken);
            if (stale.Count == 0)
            {
                return;
            }

            foreach (var scan in stale)
            {
                scan.Requeue();
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Requeued {Count} stale scan(s).", stale.Count);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
#pragma warning disable CA1031 // A transient failure must not kill the reaper loop.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            logger.LogError(exception, "Stale-scan reaping failed.");
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
