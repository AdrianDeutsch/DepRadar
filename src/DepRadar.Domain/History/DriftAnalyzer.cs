using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Domain.History;

/// <summary>
/// Compares two snapshots of the same package over time and reports what drifted —
/// the temporal counterpart of the version-to-version diff. Pure and fully testable.
/// </summary>
public static class DriftAnalyzer
{
    /// <summary>
    /// Produces the drift from <paramref name="baseline"/> to <paramref name="latest"/>.
    /// Events are ordered worst-severity first.
    /// </summary>
    public static DriftReport Compare(ScanSnapshot baseline, ScanSnapshot latest)
    {
        var before = baseline.Packages.GroupBy(p => p.Package, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var after = latest.Packages.GroupBy(p => p.Package, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var events = new List<DriftEvent>();

        foreach (var (id, current) in after)
        {
            if (!before.TryGetValue(id, out var previous))
            {
                events.Add(new DriftEvent(id, DriftEventKind.PackageAdded, $"entered the graph at {current.Version}", RiskLevel.Low));
                AddNewAdvisories(events, id, [], current.Advisories);
                continue;
            }

            CompareStates(events, id, previous, current);
        }

        foreach (var (id, previous) in before)
        {
            if (!after.ContainsKey(id))
            {
                events.Add(new DriftEvent(id, DriftEventKind.PackageRemoved, $"left the graph (was {previous.Version})", RiskLevel.None));
            }
        }

        var ordered = events
            .OrderByDescending(e => e.Severity)
            .ThenBy(e => e.Package, StringComparer.Ordinal)
            .ToList();

        return new DriftReport(
            latest.RootPackageId,
            baseline.CreatedAt,
            latest.CreatedAt,
            latest.OverallScore - baseline.OverallScore,
            ordered);
    }

    private static void CompareStates(List<DriftEvent> events, string id, PackageRiskState previous, PackageRiskState current)
    {
        if (!previous.IsDeprecated && current.IsDeprecated)
        {
            events.Add(new DriftEvent(id, DriftEventKind.BecameDeprecated, $"{current.Version} is now deprecated", RiskLevel.High));
        }

        if (!previous.IsArchived && current.IsArchived)
        {
            events.Add(new DriftEvent(id, DriftEventKind.BecameArchived, "source repository is now archived", RiskLevel.High));
        }

        if (!previous.IsStale && current.IsStale)
        {
            events.Add(new DriftEvent(id, DriftEventKind.BecameStale, "source repository is now stale", RiskLevel.Medium));
        }

        AddNewAdvisories(events, id, previous.Advisories, current.Advisories);

        foreach (var cleared in previous.Advisories.Except(current.Advisories, StringComparer.Ordinal))
        {
            events.Add(new DriftEvent(id, DriftEventKind.AdvisoryCleared, $"advisory {cleared} no longer applies", RiskLevel.None));
        }

        if (current.Score < previous.Score)
        {
            events.Add(new DriftEvent(id, DriftEventKind.HealthRegressed, $"health {previous.Score} → {current.Score}", RiskLevel.Medium));
        }
        else if (current.Score > previous.Score)
        {
            events.Add(new DriftEvent(id, DriftEventKind.HealthImproved, $"health {previous.Score} → {current.Score}", RiskLevel.None));
        }
    }

    private static void AddNewAdvisories(List<DriftEvent> events, string id, IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        foreach (var advisory in after.Except(before, StringComparer.Ordinal))
        {
            events.Add(new DriftEvent(id, DriftEventKind.BecameVulnerable, $"new advisory {advisory}", RiskLevel.High));
        }
    }
}
