using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DepRadar.Application.Risk;
using DepRadar.Domain.Risk;

namespace DepRadar.Application.Sbom;

/// <summary>
/// Renders an assessed graph as a <see href="https://cyclonedx.org/">CycloneDX</see>
/// 1.5 SBOM (JSON): components with licenses, the dependency graph, and known
/// vulnerabilities. Pure (apart from the generated serial number).
/// </summary>
public static class CycloneDxBuilder
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Builds the CycloneDX JSON document.</summary>
    public static string Build(GraphAssessment graph, DateTimeOffset generatedAt)
    {
        var components = new JsonArray();
        var vulnerabilities = new JsonArray();

        foreach (var node in graph.Nodes)
        {
            var purl = Purl(node.Package.Value, node.Version.ToString());

            var component = new JsonObject
            {
                ["type"] = "library",
                ["bom-ref"] = purl,
                ["name"] = node.Package.Original,
                ["version"] = node.Version.ToString(),
                ["purl"] = purl,
            };

            if (node.Input.ResolvedLicense is { } license)
            {
                var licenseNode = new JsonObject();
                licenseNode[license.IsRecognized ? "id" : "name"] = license.Identifier;
                component["licenses"] = new JsonArray { new JsonObject { ["license"] = licenseNode } };
            }

            components.Add(component);

            foreach (var advisory in node.Input.Vulnerabilities)
            {
                vulnerabilities.Add(new JsonObject
                {
                    ["bom-ref"] = $"{advisory.AdvisoryId}@{purl}",
                    ["id"] = advisory.AdvisoryId,
                    ["source"] = new JsonObject { ["name"] = advisory.Source },
                    ["ratings"] = new JsonArray { new JsonObject { ["severity"] = Severity(advisory.Severity) } },
                    ["description"] = advisory.Summary,
                    ["affects"] = new JsonArray { new JsonObject { ["ref"] = purl } },
                });
            }
        }

        var dependencies = new JsonArray();
        foreach (var group in graph.Edges.GroupBy(edge => Purl(edge.DependentId, edge.DependentVersion)))
        {
            var dependsOn = new JsonArray();
            foreach (var edge in group)
            {
                dependsOn.Add(Purl(edge.DependencyId, edge.DependencyVersion));
            }

            dependencies.Add(new JsonObject { ["ref"] = group.Key, ["dependsOn"] = dependsOn });
        }

        var bom = new JsonObject
        {
            ["bomFormat"] = "CycloneDX",
            ["specVersion"] = "1.5",
            ["serialNumber"] = $"urn:uuid:{Guid.NewGuid()}",
            ["version"] = 1,
            ["metadata"] = new JsonObject
            {
                ["timestamp"] = generatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                ["tools"] = new JsonArray { new JsonObject { ["vendor"] = "DepRadar", ["name"] = "DepRadar" } },
                ["component"] = new JsonObject { ["type"] = "application", ["name"] = graph.Root.Original },
            },
            ["components"] = components,
            ["dependencies"] = dependencies,
            ["vulnerabilities"] = vulnerabilities,
        };

        return bom.ToJsonString(Options);
    }

    private static string Purl(string id, string version) => $"pkg:nuget/{id}@{version}";

    private static string Severity(RiskLevel level) => level switch
    {
        RiskLevel.Critical => "critical",
        RiskLevel.High => "high",
        RiskLevel.Medium => "medium",
        RiskLevel.Low => "low",
        _ => "none",
    };
}
