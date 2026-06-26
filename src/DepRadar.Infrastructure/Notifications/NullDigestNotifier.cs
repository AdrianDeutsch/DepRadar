using DepRadar.Application.History;

namespace DepRadar.Infrastructure.Notifications;

/// <summary>Default no-op digest notifier used when no delivery channel is configured.</summary>
internal sealed class NullDigestNotifier : IDigestNotifier
{
    /// <inheritdoc />
    public Task DeliverAsync(string markdown, CancellationToken cancellationToken) => Task.CompletedTask;
}
