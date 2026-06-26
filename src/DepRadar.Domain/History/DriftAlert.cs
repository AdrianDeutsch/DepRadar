using DepRadar.Domain.Risk;

namespace DepRadar.Domain.History;

/// <summary>
/// Decides which drift is worth paging someone about: the newly-introduced,
/// high-severity risks (a package that became vulnerable, deprecated or archived).
/// Improvements and low-severity churn are deliberately excluded so alerts stay signal.
/// </summary>
public static class DriftAlert
{
    /// <summary>The high-severity events from a report (empty = nothing to alert on).</summary>
    public static IReadOnlyList<DriftEvent> Actionable(DriftReport report) =>
        report.Events.Where(e => e.Severity >= RiskLevel.High).ToList();
}
