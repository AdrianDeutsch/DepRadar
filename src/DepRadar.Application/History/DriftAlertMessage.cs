using System.Globalization;
using DepRadar.Domain.History;

namespace DepRadar.Application.History;

/// <summary>
/// Formats an actionable drift report into a short chat-ready message (Slack mrkdwn).
/// Pure so the wording is unit-testable independently of any transport.
/// </summary>
public static class DriftAlertMessage
{
    /// <summary>Builds the alert text for the high-severity events in <paramref name="report"/>.</summary>
    public static string Format(DriftReport report)
    {
        var actionable = DriftAlert.Actionable(report);
        var delta = report.NetHealthDelta.ToString("+0;-0;0", CultureInfo.InvariantCulture);

        var header = $":rotating_light: *DepRadar drift* for `{report.Root.Value}` — "
            + $"{actionable.Count} new high-severity issue(s), net health {delta}:";

        var lines = actionable.Select(e => $"• *{e.Package}* — {e.Detail}");

        return string.Join("\n", lines.Prepend(header));
    }
}
