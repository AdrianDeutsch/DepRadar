using DepRadar.Application.Abstractions;

namespace DepRadar.Worker.Pipeline;

/// <summary>
/// The pipeline's producer: periodically claims queued scans from the database and
/// feeds them into the <see cref="ScanDispatchQueue"/>. Polling the DB (rather than
/// an in-process call from the API) keeps the API and worker fully decoupled and
/// survives restarts — the queue is durable in Postgres.
/// </summary>
internal sealed class ScanPollingService(
    ScanDispatchQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ScanPollingService> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 32;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        logger.LogInformation("Scan polling started (every {Seconds}s).", PollInterval.TotalSeconds);

        do
        {
            await PollOnceAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var scans = scope.ServiceProvider.GetRequiredService<IScanRepository>();

            var queued = await scans.GetQueuedAsync(BatchSize, stoppingToken);
            foreach (var scanId in queued)
            {
                await queue.TryEnqueueAsync(scanId, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown — let the loop exit.
        }
#pragma warning disable CA1031 // A transient poll failure must not kill the producer loop.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            logger.LogError(exception, "Scan polling iteration failed.");
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
