using DepRadar.Application.History;
using DepRadar.Application.Messaging;

namespace DepRadar.Worker.Pipeline;

/// <summary>
/// Periodically delivers the cross-package drift digest to the configured channel
/// (e.g. Slack). Opt-in via <c>Digest:IntervalHours</c>; it stays quiet when nothing
/// has drifted, so a clean week produces no noise.
/// </summary>
internal sealed class DigestScheduleService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DigestScheduleService> logger)
    : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hours = configuration.GetValue<double>("Digest:IntervalHours");
        if (hours <= 0)
        {
            return; // scheduled digest is opt-in
        }

        logger.LogInformation("Scheduled drift digest enabled: every {Hours}h.", hours);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(hours));
        while (await WaitAsync(timer, stoppingToken))
        {
            await DeliverAsync(stoppingToken);
        }
    }

    private async Task DeliverAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var notifier = scope.ServiceProvider.GetRequiredService<IDigestNotifier>();

            var markdown = await sender.Send(new GetDriftDigestQuery(), cancellationToken);

            // A per-root drift section starts with "## "; without one nothing changed.
            if (!markdown.Contains("## ", StringComparison.Ordinal))
            {
                return;
            }

            await notifier.DeliverAsync(markdown, cancellationToken);
            logger.LogInformation("Drift digest delivered.");
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
#pragma warning disable CA1031 // A transient failure must not kill the digest loop.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            logger.LogError(exception, "Drift digest delivery failed.");
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
