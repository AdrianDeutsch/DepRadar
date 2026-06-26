using DepRadar.Domain.History;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.History;

/// <summary>
/// Port for pushing drift to an outside channel (e.g. a Slack webhook or GitHub issue).
/// Implementations are best-effort and must not throw into the caller — a failed
/// notification never fails a scan.
/// </summary>
public interface IDriftNotifier
{
    /// <summary>Reports actionable, high-severity drift described by <paramref name="report"/>.</summary>
    Task NotifyAsync(DriftReport report, CancellationToken cancellationToken);

    /// <summary>
    /// Signals that a previously-reported drift for <paramref name="root"/> has cleared —
    /// channels that track state (e.g. GitHub issues) close it; stateless channels no-op.
    /// </summary>
    Task ResolveAsync(PackageId root, CancellationToken cancellationToken);
}
