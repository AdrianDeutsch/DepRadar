using System.Globalization;
using System.Text;
using DepRadar.Domain.History;

namespace DepRadar.Application.History;

/// <summary>
/// Renders a Markdown drift digest across every tracked package — a single, shareable
/// "what changed in my dependencies" report (e.g. a daily summary).
/// </summary>
public static class DriftDigestBuilder
{
    /// <summary>Builds the digest from per-root drift reports, taken at <paramref name="generatedAt"/>.</summary>
    public static string Render(IReadOnlyList<DriftReport> reports, DateTimeOffset generatedAt)
    {
        var withDrift = reports
            .Where(r => r.Events.Count > 0)
            .OrderBy(r => r.NetHealthDelta) // worst (most negative) first
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("# DepRadar drift digest").AppendLine();
        builder.Append("_Generated ")
            .Append(generatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))
            .Append(" · ")
            .Append(reports.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine(" package(s) tracked_").AppendLine();

        if (withDrift.Count == 0)
        {
            builder.AppendLine("No drift detected since the previous scan. :tada:");
            return builder.ToString();
        }

        foreach (var report in withDrift)
        {
            var delta = report.NetHealthDelta.ToString("+0;-0;0", CultureInfo.InvariantCulture);
            builder.Append("## ").Append(report.Root.Value).Append(" (net health ").Append(delta).AppendLine(")").AppendLine();

            foreach (var change in report.Events)
            {
                builder.Append("- **").Append(change.Package).Append("** ")
                    .Append(change.Kind).Append(": ").AppendLine(change.Detail);
            }

            builder.AppendLine();
        }

        var unchanged = reports.Count - withDrift.Count;
        if (unchanged > 0)
        {
            builder.Append('_').Append(unchanged.ToString(CultureInfo.InvariantCulture)).AppendLine(" package(s) unchanged._");
        }

        return builder.ToString();
    }
}
