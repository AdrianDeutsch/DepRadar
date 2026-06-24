namespace DepRadar.Worker.Workers;

/// <summary>
/// Background ingestion service. In Slice 1 it only signals readiness; the
/// decoupled, idempotent ingestion pipeline built on
/// <see cref="System.Threading.Channels"/> is delivered in Slice 2.
/// </summary>
internal sealed class IngestionWorker(ILogger<IngestionWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "DepRadar ingestion worker started. The Channel-based ingestion pipeline is delivered in Slice 2.");
        return Task.CompletedTask;
    }
}
