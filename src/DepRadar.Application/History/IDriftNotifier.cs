using DepRadar.Domain.History;

namespace DepRadar.Application.History;

/// <summary>
/// Port for pushing a drift alert to an outside channel (e.g. a Slack webhook).
/// Called only when there is actionable, high-severity drift. Implementations must be
/// best-effort and must not throw — a failed notification never fails a scan.
/// </summary>
public interface IDriftNotifier
{
    /// <summary>Notifies about the drift described by <paramref name="report"/>.</summary>
    Task NotifyAsync(DriftReport report, CancellationToken cancellationToken);
}
