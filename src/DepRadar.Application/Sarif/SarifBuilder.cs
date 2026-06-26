using System.Text.Json;
using System.Text.Json.Nodes;
using DepRadar.Application.Graphs;
using DepRadar.Application.Risk;
using DepRadar.Domain.Risk;

namespace DepRadar.Application.Sarif;

/// <summary>
/// Renders an assessed graph as a <see href="https://sarifweb.azurewebsites.net/">SARIF</see>
/// 2.1.0 document — the format GitHub code scanning ingests, so DepRadar findings show up
/// in the repository's <em>Security</em> tab. Pure.
/// </summary>
public static class SarifBuilder
{
    private const string ToolName = "DepRadar";
    private const string InformationUri = "https://github.com/AdrianDeutsch/DepRadar";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>
    /// Builds the SARIF JSON. Results point at <paramref name="manifestUri"/> (the scanned
    /// project file, or the package id) since dependency findings have no source line.
    /// </summary>
    public static string Build(GraphAssessment graph, string manifestUri)
    {
        var rules = new Dictionary<string, RiskFinding>(StringComparer.Ordinal);
        var results = new JsonArray();

        foreach (var node in graph.Nodes)
        {
            var coordinate = $"{node.Package.Value}@{node.Version}";
            var path = node.Input.Vulnerabilities.Count > 0
                ? DependencyPathFinder.ShortestPath(graph.Edges, graph.Root.Value, node.Package.Value)
                : null;

            foreach (var finding in node.Assessment.Findings)
            {
                rules.TryAdd(finding.Code, finding);

                var text = $"{coordinate}: {finding.Message}";
                if (path is { Count: > 1 })
                {
                    text += $" (pulled in via {string.Join(" → ", path)})";
                }

                results.Add(Result(finding, text, manifestUri, coordinate));
            }
        }

        var ruleArray = new JsonArray();
        foreach (var (code, sample) in rules)
        {
            ruleArray.Add(Rule(code, sample));
        }

        var sarif = new JsonObject
        {
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"] = new JsonArray
            {
                new JsonObject
                {
                    ["tool"] = new JsonObject
                    {
                        ["driver"] = new JsonObject
                        {
                            ["name"] = ToolName,
                            ["informationUri"] = InformationUri,
                            ["rules"] = ruleArray,
                        },
                    },
                    ["results"] = results,
                },
            },
        };

        return sarif.ToJsonString(Options);
    }

    private static JsonObject Rule(string code, RiskFinding sample) => new()
    {
        ["id"] = code,
        ["name"] = code,
        ["shortDescription"] = new JsonObject { ["text"] = $"{sample.Category} risk ({code})" },
        ["defaultConfiguration"] = new JsonObject { ["level"] = Level(sample.Level) },
        ["helpUri"] = InformationUri,
    };

    private static JsonObject Result(RiskFinding finding, string text, string manifestUri, string coordinate) => new()
    {
        ["ruleId"] = finding.Code,
        ["level"] = Level(finding.Level),
        ["message"] = new JsonObject { ["text"] = text },
        ["locations"] = new JsonArray
        {
            new JsonObject
            {
                ["physicalLocation"] = new JsonObject
                {
                    ["artifactLocation"] = new JsonObject { ["uri"] = manifestUri },
                    ["region"] = new JsonObject { ["startLine"] = 1 },
                },
            },
        },
        ["partialFingerprints"] = new JsonObject { ["depRadar/v1"] = $"{coordinate}:{finding.Code}" },
    };

    // SARIF severities: error fails code-scanning gates, warning/note inform.
    private static string Level(RiskLevel level) => level switch
    {
        RiskLevel.Critical or RiskLevel.High => "error",
        RiskLevel.Medium => "warning",
        _ => "note",
    };
}
