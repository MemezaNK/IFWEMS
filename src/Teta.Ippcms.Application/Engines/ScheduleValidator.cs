namespace Teta.Ippcms.Application.Engines;

/// <summary>Schedule dependency validation (FR-EXE-003): detects dependency loops.</summary>
public static class ScheduleValidator
{
    /// <summary>Returns one cycle (list of node IDs) if the dependency graph contains a loop, otherwise null.</summary>
    public static IReadOnlyList<Guid>? FindCycle(IEnumerable<(Guid Predecessor, Guid Successor)> edges)
    {
        var graph = new Dictionary<Guid, List<Guid>>();
        foreach (var (p, s) in edges)
        {
            if (!graph.TryGetValue(p, out var list)) graph[p] = list = new List<Guid>();
            list.Add(s);
            if (!graph.ContainsKey(s)) graph[s] = new List<Guid>();
        }

        var state = new Dictionary<Guid, int>(); // 0 = unvisited, 1 = on stack, 2 = done
        var stack = new List<Guid>();

        foreach (var node in graph.Keys)
        {
            var cycle = Visit(node);
            if (cycle is not null) return cycle;
        }
        return null;

        List<Guid>? Visit(Guid node)
        {
            if (state.TryGetValue(node, out var s))
            {
                if (s == 1)
                {
                    var start = stack.IndexOf(node);
                    return stack.Skip(start).Append(node).ToList();
                }
                if (s == 2) return null;
            }

            state[node] = 1;
            stack.Add(node);
            foreach (var next in graph[node])
            {
                var cycle = Visit(next);
                if (cycle is not null) return cycle;
            }
            stack.RemoveAt(stack.Count - 1);
            state[node] = 2;
            return null;
        }
    }

    /// <summary>
    /// Weighted roll-up of percent complete from children to parent (FR-EXE-001 "hierarchy supports roll-up").
    /// </summary>
    public static decimal RollUp(IEnumerable<(decimal Percent, decimal Weight)> children)
    {
        var list = children.ToList();
        var totalWeight = list.Sum(c => c.Weight);
        if (list.Count == 0 || totalWeight <= 0) return 0m;
        return Math.Round(list.Sum(c => c.Percent * c.Weight) / totalWeight, 1);
    }
}
