using DepRadar.Application.History;
using DepRadar.Domain.History;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Infrastructure.Notifications;

/// <summary>
/// Fans drift notifications out to every configured channel (Slack, GitHub, …). Each
/// channel is attempted; the aggregate is awaited so one flaky channel never blocks the others.
/// </summary>
internal sealed class CompositeDriftNotifier(IReadOnlyList<IDriftNotifier> channels) : IDriftNotifier
{
    /// <inheritdoc />
    public Task NotifyAsync(DriftReport report, CancellationToken cancellationToken) =>
        Task.WhenAll(channels.Select(channel => channel.NotifyAsync(report, cancellationToken)));

    /// <inheritdoc />
    public Task ResolveAsync(PackageId root, CancellationToken cancellationToken) =>
        Task.WhenAll(channels.Select(channel => channel.ResolveAsync(root, cancellationToken)));
}
