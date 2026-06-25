using DepRadar.Application.Risk;
using DepRadar.Domain.Risk;

namespace DepRadar.Application.Diff;

/// <summary>
/// Diffs two assessed graphs of the same package at different versions. Pure and
/// source-agnostic, so an upgrade-impact diff is just "analyze both, then diff".
/// </summary>
public static class GraphDiffer
{
    /// <summary>Computes the upgrade impact of moving from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static UpgradeDiff Diff(GraphAssessment from, GraphAssessment to)
    {
        var fromNodes = IndexByPackage(from);
        var toNodes = IndexByPackage(to);

        var added = toNodes.Keys.Except(fromNodes.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => Coordinate(toNodes[key]))
            .ToList();

        var removed = fromNodes.Keys.Except(toNodes.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => Coordinate(fromNodes[key]))
            .ToList();

        var changed = fromNodes.Keys.Intersect(toNodes.Keys, StringComparer.Ordinal)
            .Where(key => !fromNodes[key].Version.Equals(toNodes[key].Version))
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => new VersionChange(key, fromNodes[key].Version.ToString(), toNodes[key].Version.ToString()))
            .ToList();

        var fromAdvisories = IndexAdvisories(from);
        var toAdvisories = IndexAdvisories(to);

        var newAdvisories = toAdvisories.Keys.Except(fromAdvisories.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => toAdvisories[key])
            .ToList();

        var resolvedAdvisories = fromAdvisories.Keys.Except(toAdvisories.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => fromAdvisories[key])
            .ToList();

        var (fromScore, fromLevel) = Overall(from);
        var (toScore, toLevel) = Overall(to);

        return new UpgradeDiff(
            from.Root.Value,
            RootVersion(from),
            RootVersion(to),
            fromScore,
            fromLevel.ToString(),
            toScore,
            toLevel.ToString(),
            added,
            removed,
            changed,
            newAdvisories,
            resolvedAdvisories);
    }

    private static Dictionary<string, AssessedNode> IndexByPackage(GraphAssessment graph) =>
        graph.Nodes
            .GroupBy(node => node.Package.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    /// <summary>Advisories keyed by (package, advisory) so set-difference yields new/cleared ones.</summary>
    private static Dictionary<string, string> IndexAdvisories(GraphAssessment graph)
    {
        var advisories = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            foreach (var vulnerability in node.Input.Vulnerabilities)
            {
                var key = $"{node.Package.Value}|{vulnerability.AdvisoryId}";
                advisories[key] = $"{vulnerability.AdvisoryId} in {Coordinate(node)}";
            }
        }

        return advisories;
    }

    private static string RootVersion(GraphAssessment graph)
    {
        var rootNode = graph.Nodes.FirstOrDefault(node => node.Package.Equals(graph.Root));
        return rootNode?.Version.ToString() ?? string.Empty;
    }

    private static string Coordinate(AssessedNode node) => $"{node.Package.Value}@{node.Version}";

    private static (int Score, RiskLevel Level) Overall(GraphAssessment graph)
    {
        if (graph.Nodes.Count == 0)
        {
            return (100, RiskLevel.None);
        }

        return (graph.Nodes.Min(node => node.Assessment.Score.Value), graph.Nodes.Max(node => node.Assessment.Score.Level));
    }
}
