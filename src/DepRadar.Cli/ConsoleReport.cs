using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DepRadar.Application.Policy;
using DepRadar.Application.Risk;
using DepRadar.Domain.Risk;

namespace DepRadar.Cli;

/// <summary>Renders an assessed graph + policy outcome to the console (text or JSON).</summary>
internal static class ConsoleReport
{
    private const int TopFindings = 12;

    /// <summary>Writes a human-readable report to stdout.</summary>
    public static void WriteText(GraphAssessment graph, PolicyOutcome outcome, IReadOnlyList<string> unresolved, IReadOnlyList<string>? warnings = null)
    {
        var (score, level) = Overall(graph);

        Console.WriteLine();
        Console.WriteLine($"DepRadar — {graph.Root.Value}");
        Console.WriteLine($"  packages: {graph.Nodes.Count}    health: {score}/100 ({level})");
        if (graph.Truncated)
        {
            // A capped graph is incomplete — risk in the unexplored tail is invisible.
            Console.WriteLine("  ! graph truncated at the node cap — results are partial; risk may be understated.");
        }

        if (unresolved.Count > 0)
        {
            Console.WriteLine($"  unresolved: {string.Join(", ", unresolved)}");
        }

        foreach (var warning in warnings ?? [])
        {
            Console.WriteLine($"  ! {warning}");
        }

        Console.WriteLine();
        Console.WriteLine("  Risk ranking:");
        var ranked = graph.Nodes
            .OrderByDescending(n => n.Assessment.Score.Level)
            .ThenBy(n => n.Assessment.Score.Value)
            .Take(TopFindings);

        foreach (var node in ranked)
        {
            var codes = node.Assessment.Findings.Count == 0
                ? "ok"
                : string.Join(", ", node.Assessment.Findings.Select(f => f.Code).Distinct());
            var coordinate = $"{node.Package.Value} {node.Version}";
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"    {node.Assessment.Score.Level,-8} {node.Assessment.Score.Value,3}/100  {coordinate,-42} {codes}"));
        }

        Console.WriteLine();
        if (outcome.Passed)
        {
            Console.WriteLine("  Policy: PASSED");
        }
        else
        {
            Console.WriteLine($"  Policy: FAILED ({outcome.Violations.Count} violation(s))");
            foreach (var violation in outcome.Violations)
            {
                Console.WriteLine($"    x {violation.Package} — {violation.Reason}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>Writes a machine-readable JSON report to stdout.</summary>
    public static void WriteJson(GraphAssessment graph, PolicyOutcome outcome, IReadOnlyList<string> unresolved, IReadOnlyList<string>? warnings = null)
    {
        var (score, level) = Overall(graph);

        var packages = new JsonArray();
        foreach (var node in graph.Nodes.OrderByDescending(n => n.Assessment.Score.Level).ThenBy(n => n.Assessment.Score.Value))
        {
            packages.Add(new JsonObject
            {
                ["id"] = node.Package.Value,
                ["version"] = node.Version.ToString(),
                ["score"] = node.Assessment.Score.Value,
                ["level"] = node.Assessment.Score.Level.ToString(),
                ["findings"] = new JsonArray(node.Assessment.Findings.Select(f => (JsonNode)f.Code).ToArray()),
            });
        }

        var violations = new JsonArray();
        foreach (var violation in outcome.Violations)
        {
            violations.Add(new JsonObject { ["package"] = violation.Package, ["reason"] = violation.Reason });
        }

        var root = new JsonObject
        {
            ["root"] = graph.Root.Value,
            ["packageCount"] = graph.Nodes.Count,
            ["truncated"] = graph.Truncated,
            ["overallScore"] = score,
            ["overallLevel"] = level.ToString(),
            ["passed"] = outcome.Passed,
            ["unresolved"] = new JsonArray(unresolved.Select(u => (JsonNode)u).ToArray()),
            ["warnings"] = new JsonArray((warnings ?? []).Select(w => (JsonNode)w).ToArray()),
            ["packages"] = packages,
            ["violations"] = violations,
        };

        Console.WriteLine(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Overall score = the worst node's score; overall level = the worst node's level.</summary>
    private static (int Score, RiskLevel Level) Overall(GraphAssessment graph)
    {
        if (graph.Nodes.Count == 0)
        {
            return (100, RiskLevel.None);
        }

        return (graph.Nodes.Min(n => n.Assessment.Score.Value), graph.Nodes.Max(n => n.Assessment.Score.Level));
    }
}
