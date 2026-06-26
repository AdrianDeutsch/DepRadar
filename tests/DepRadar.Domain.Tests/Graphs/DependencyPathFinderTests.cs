using DepRadar.Application.Abstractions;
using DepRadar.Application.Graphs;
using Shouldly;
using Xunit;

namespace DepRadar.Domain.Tests.Graphs;

public sealed class DependencyPathFinderTests
{
    [Fact]
    public void Finds_the_shortest_transitive_path()
    {
        // root -> a -> b -> vuln, plus a shortcut root -> c -> vuln (shorter)
        var edges = new List<GraphEdgeRow>
        {
            Edge("root", "a"),
            Edge("a", "b"),
            Edge("b", "vuln"),
            Edge("root", "c"),
            Edge("c", "vuln"),
        };

        var path = DependencyPathFinder.ShortestPath(edges, "root", "vuln");

        path.ShouldBe(["root", "c", "vuln"]);
    }

    [Fact]
    public void Returns_the_root_alone_when_the_target_is_the_root()
    {
        DependencyPathFinder.ShortestPath([], "root", "root").ShouldBe(["root"]);
    }

    [Fact]
    public void Returns_null_when_the_target_is_unreachable()
    {
        var edges = new List<GraphEdgeRow> { Edge("root", "a") };

        DependencyPathFinder.ShortestPath(edges, "root", "ghost").ShouldBeNull();
    }

    private static GraphEdgeRow Edge(string from, string to) =>
        new(from, "1.0.0", to, "1.0.0", "[1.0.0, )", IsDirect: from == "root", Depth: 1);
}
