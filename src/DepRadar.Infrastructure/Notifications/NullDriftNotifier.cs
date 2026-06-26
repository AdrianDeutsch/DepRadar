using DepRadar.Application.History;
using DepRadar.Domain.History;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Infrastructure.Notifications;

/// <summary>
/// Default no-op notifier used when no alert webhook is configured. Keeps drift
/// alerting keyless-by-default, exactly like the language-model seam.
/// </summary>
internal sealed class NullDriftNotifier : IDriftNotifier
{
    /// <inheritdoc />
    public Task NotifyAsync(DriftReport report, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task ResolveAsync(PackageId root, CancellationToken cancellationToken) => Task.CompletedTask;
}
