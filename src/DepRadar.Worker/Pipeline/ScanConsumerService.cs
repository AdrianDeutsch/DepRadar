using DepRadar.Application.Messaging;
using DepRadar.Application.Scans;

namespace DepRadar.Worker.Pipeline;

/// <summary>
/// The pipeline's consumer: drains scan ids from the <see cref="ScanDispatchQueue"/>
/// and runs each one through the mediator in its own DI scope. The handler owns the
/// scan lifecycle, so a single failing scan never stops the pipeline.
/// </summary>
internal sealed class ScanConsumerService(
    ScanDispatchQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ScanConsumerService> logger)
    : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var scanId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new RunScanCommand(scanId.Value), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
#pragma warning disable CA1031 // The handler marks the scan Failed; keep the consumer alive regardless.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                logger.LogError(exception, "Dispatching scan {ScanId} failed.", scanId.Value);
            }
            finally
            {
                queue.Release(scanId);
            }
        }
    }
}
