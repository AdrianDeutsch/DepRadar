using System.Globalization;
using System.Text;
using DepRadar.Domain.History;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.History;

/// <summary>
/// Formats an actionable drift report into a GitHub issue (title + Markdown body).
/// Pure, so the wording is testable independently of the GitHub client.
/// </summary>
public static class DriftIssue
{
    /// <summary>
    /// A <em>stable</em> issue title (one per root), so successive alerts can find and
    /// update — or close — the existing open issue instead of opening a new one each time.
    /// </summary>
    public static string Title(PackageId root) =>
        string.Create(CultureInfo.InvariantCulture, $"DepRadar: drift in {root.Value}");

    /// <inheritdoc cref="Title(PackageId)"/>
    public static string Title(DriftReport report) => Title(report.Root);

    /// <summary>The comment posted when the issue is auto-closed because drift subsided.</summary>
    public static string ResolvedComment() =>
        ":white_check_mark: **Drift resolved** — the latest scan found no high-severity issues.\n\n_Closed automatically by DepRadar._";

    /// <summary>The issue (or comment) body in GitHub-flavored Markdown.</summary>
    public static string Body(DriftReport report)
    {
        var actionable = DriftAlert.Actionable(report);
        var delta = report.NetHealthDelta.ToString("+0;-0;0", CultureInfo.InvariantCulture);
        var asOf = report.To.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

        var builder = new StringBuilder();
        builder.Append("**").Append(actionable.Count.ToString(CultureInfo.InvariantCulture))
            .Append(" new high-severity issue(s)** in `").Append(report.Root.Value)
            .Append("` (net health ").Append(delta).Append("), as of ").Append(asOf).AppendLine(":").AppendLine();

        foreach (var change in actionable)
        {
            builder.Append("- **").Append(change.Package).Append("** — ").AppendLine(change.Detail);
        }

        builder.AppendLine().AppendLine("_Reported automatically by DepRadar._");
        return builder.ToString();
    }
}
