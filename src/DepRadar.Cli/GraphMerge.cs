using DepRadar.Application.Risk;

namespace DepRadar.Cli;

/// <summary>
/// Unions per-root <see cref="GraphAssessment"/>s into one graph — used whenever a
/// scan target expands to multiple roots (a .csproj, a package.json, a requirements.txt).
/// </summary>
internal static class GraphMerge
{
    /// <summary>Distinct union of nodes and edges; truncated if any part was.</summary>
    public static GraphAssessment Union(IReadOnlyList<GraphAssessment> assessments)
    {
        if (assessments.Count == 1)
        {
            return assessments[0];
        }

        var nodes = assessments
            .SelectMany(a => a.Nodes)
            .GroupBy(n => (n.Package.Value, n.Version.ToString()))
            .Select(g => g.First())
            .ToList();

        var edges = assessments
            .SelectMany(a => a.Edges)
            .GroupBy(e => (e.DependentId, e.DependentVersion, e.DependencyId, e.DependencyVersion))
            .Select(g => g.First())
            .ToList();

        return new GraphAssessment(assessments[0].Root, nodes, edges, assessments.Any(a => a.Truncated));
    }
}
