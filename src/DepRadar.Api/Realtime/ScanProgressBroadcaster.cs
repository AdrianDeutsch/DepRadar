using DepRadar.Application.Abstractions;
using DepRadar.Application.Scans;
using DepRadar.Domain.Packages;
using DepRadar.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace DepRadar.Api.Realtime;

/// <summary>
/// Bridges the out-of-process worker pipeline to SignalR clients. Postgres is the
/// source of truth (the worker writes scan status there), so this service polls for
/// status changes and pushes them to the relevant scan group — no broker, and it
/// works across the API/Worker process split.
/// </summary>
internal sealed class ScanProgressBroadcaster(
    IServiceScopeFactory scopeFactory,
    IHubContext<ScanHub> hubContext,
    ILogger<ScanProgressBroadcaster> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private readonly Dictionary<Guid, ScanStatus> _lastStatus = [];

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await WaitAsync(timer, stoppingToken))
        {
            await BroadcastChangesAsync(stoppingToken);
        }
    }

    private async Task BroadcastChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var scans = scope.ServiceProvider.GetRequiredService<IScanRepository>();

            var active = await scans.GetActiveAsync(100, cancellationToken);
            var activeIds = active.Select(scan => scan.Id.Value).ToHashSet();

            foreach (var scan in active)
            {
                await PushIfChangedAsync(scan, cancellationToken);
            }

            // Scans no longer active have reached a terminal state — push it once.
            foreach (var id in _lastStatus.Keys.Where(id => !activeIds.Contains(id)).ToList())
            {
                var finished = await scans.GetAsync(ScanId.From(id), cancellationToken);
                if (finished is not null)
                {
                    await PushAsync(finished, cancellationToken);
                }

                _lastStatus.Remove(id);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
#pragma warning disable CA1031 // A transient poll failure must not kill the broadcaster.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            logger.LogError(exception, "Scan progress broadcast failed.");
        }
    }

    private async Task PushIfChangedAsync(Scan scan, CancellationToken cancellationToken)
    {
        if (_lastStatus.TryGetValue(scan.Id.Value, out var previous) && previous == scan.Status)
        {
            return;
        }

        _lastStatus[scan.Id.Value] = scan.Status;
        await PushAsync(scan, cancellationToken);
    }

    private Task PushAsync(Scan scan, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(scan.Id.Value.ToString())
            .SendAsync("ScanUpdated", ScanDto.FromDomain(scan), cancellationToken);

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
