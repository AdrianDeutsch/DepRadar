using System.Globalization;
using System.Text;
using DepRadar.Domain.History;

namespace DepRadar.Application.History;

/// <summary>
/// Formats an actionable drift report into a GitHub issue (title + Markdown body).
/// Pure, so the wording is testable independently of the GitHub client.
/// </summary>
public static class DriftIssue
{
    /// <summary>The issue title.</summary>
    public static string Title(DriftReport report) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"DepRadar: drift in {report.Root.Value} ({DriftAlert.Actionable(report).Count} new high-severity issue(s))");

    /// <summary>The issue body in GitHub-flavored Markdown.</summary>
    public static string Body(DriftReport report)
    {
        var delta = report.NetHealthDelta.ToString("+0;-0;0", CultureInfo.InvariantCulture);

        var builder = new StringBuilder();
        builder.Append("DepRadar detected drift in `").Append(report.Root.Value)
            .Append("` since the previous scan (net health ").Append(delta).AppendLine(").").AppendLine();
        builder.AppendLine("**New high-severity issues:**").AppendLine();

        foreach (var change in DriftAlert.Actionable(report))
        {
            builder.Append("- **").Append(change.Package).Append("** — ").AppendLine(change.Detail);
        }

        builder.AppendLine().AppendLine("_Reported automatically by DepRadar._");
        return builder.ToString();
    }
}
