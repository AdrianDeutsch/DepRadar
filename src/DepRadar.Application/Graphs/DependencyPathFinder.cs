using DepRadar.Application.Abstractions;

namespace DepRadar.Application.Graphs;

/// <summary>
/// Finds the shortest dependency path from a root package to a target — the answer to
/// "why is this (vulnerable) package in my graph?". Pure breadth-first search over the
/// resolved edges, keyed by package id.
/// </summary>
public static class DependencyPathFinder
{
    /// <summary>
    /// The shortest chain of package ids from <paramref name="rootId"/> to
    /// <paramref name="targetId"/> (inclusive), or <see langword="null"/> if the target
    /// is unreachable.
    /// </summary>
    public static IReadOnlyList<string>? ShortestPath(
        IReadOnlyList<GraphEdgeRow> edges,
        string rootId,
        string targetId)
    {
        if (string.Equals(rootId, targetId, StringComparison.Ordinal))
        {
            return [rootId];
        }

        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (!adjacency.TryGetValue(edge.DependentId, out var neighbours))
            {
                neighbours = [];
                adjacency[edge.DependentId] = neighbours;
            }

            neighbours.Add(edge.DependencyId);
        }

        var predecessor = new Dictionary<string, string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(rootId);
        predecessor[rootId] = rootId;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbours))
            {
                continue;
            }

            foreach (var next in neighbours)
            {
                if (predecessor.ContainsKey(next))
                {
                    continue;
                }

                predecessor[next] = current;
                if (string.Equals(next, targetId, StringComparison.Ordinal))
                {
                    return Reconstruct(predecessor, rootId, targetId);
                }

                queue.Enqueue(next);
            }
        }

        return null;
    }

    private static List<string> Reconstruct(Dictionary<string, string> predecessor, string rootId, string targetId)
    {
        var path = new List<string> { targetId };
        var current = targetId;
        while (!string.Equals(current, rootId, StringComparison.Ordinal))
        {
            current = predecessor[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
