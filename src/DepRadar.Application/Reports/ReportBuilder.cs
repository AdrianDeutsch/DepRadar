using System.Globalization;
using System.Text;
using DepRadar.Application.Risk;
using DepRadar.Application.Upgrades;

namespace DepRadar.Application.Reports;

/// <summary>
/// Renders an audit-ready Markdown report from a graph-risk rollup and the root's
/// upgrade advice. Pure and unit-tested.
/// </summary>
public static class ReportBuilder
{
    private const int MaxRows = 50;

    /// <summary>Builds the Markdown report.</summary>
    public static string BuildMarkdown(GraphRiskDto risk, UpgradeAdviceDto? upgrade, DateTimeOffset generatedAt)
    {
        var report = new StringBuilder();

        report.Append(CultureInfo.InvariantCulture, $"# DepRadar report — {risk.Root}\n\n");
        report.Append(CultureInfo.InvariantCulture, $"_Generated {generatedAt:yyyy-MM-dd HH:mm} UTC_\n\n");

        report.Append("## Summary\n\n");
        report.Append(CultureInfo.InvariantCulture, $"- **Project health:** {risk.OverallScore}/100 ({risk.OverallLevel})\n");
        report.Append(CultureInfo.InvariantCulture, $"- **Packages assessed:** {risk.PackagesAssessed}\n\n");

        report.Append("## Risk ranking (worst first)\n\n");
        report.Append("| Package | Version | Score | Level | Findings |\n");
        report.Append("| --- | --- | ---: | --- | --- |\n");
        foreach (var package in risk.Packages.Take(MaxRows))
        {
            var findings = package.Findings.Count == 0
                ? "—"
                : string.Join(", ", package.Findings.Select(f => $"{f.Code} ({f.Level})"));
            report.Append(CultureInfo.InvariantCulture,
                $"| {package.PackageId} | {package.Version} | {package.Score} | {package.Level} | {findings} |\n");
        }

        if (risk.Packages.Count > MaxRows)
        {
            report.Append(CultureInfo.InvariantCulture, $"\n_…and {risk.Packages.Count - MaxRows} more._\n");
        }

        if (upgrade is not null)
        {
            report.Append(CultureInfo.InvariantCulture, $"\n## Upgrade advice — {upgrade.FromVersion} → {upgrade.ToVersion}\n\n");
            report.Append(CultureInfo.InvariantCulture, $"- **Recommendation:** {upgrade.Recommendation}\n\n");
            report.Append(upgrade.Narrative);
            report.Append('\n');
        }

        return report.ToString();
    }
}
