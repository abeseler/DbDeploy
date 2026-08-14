namespace Ratchet.Models;

internal static class MigrationOrderResolver
{
    public static Result<List<Migration>> Resolve(IReadOnlyList<Migration> migrations, string[] contexts, IReadOnlyCollection<string> appliedFileNames)
    {
        var appliedKeys = appliedFileNames.Select(Key).ToHashSet();
        var count = migrations.Count;
        var inContext = new bool[count];
        var filesByKey = new Dictionary<string, List<int>>();
        var distinctNamesByKey = new Dictionary<string, HashSet<string>>();

        for (var i = 0; i < count; i++)
        {
            inContext[i] = migrations[i].IsMissingRequiredContext(contexts) is false;
            var key = Key(migrations[i].FileName);
            if (filesByKey.TryGetValue(key, out var indices) is false)
            {
                indices = [];
                filesByKey[key] = indices;
                distinctNamesByKey[key] = [];
            }
            indices.Add(i);
            distinctNamesByKey[key].Add(migrations[i].FileName);
        }

        var adjacency = new List<int>[count];
        var predecessors = new List<int>[count];
        var indegree = new int[count];
        for (var i = 0; i < count; i++)
        {
            adjacency[i] = [];
            predecessors[i] = [];
        }

        var edges = new HashSet<(int From, int To)>();
        for (var i = 0; i < count; i++)
        {
            if (inContext[i] is false || migrations[i].DependsOn.Length == 0)
                continue;

            foreach (var reference in migrations[i].DependsOn)
            {
                var key = Key(reference);
                if (filesByKey.TryGetValue(key, out var targets) is false)
                {
                    if (appliedKeys.Contains(key))
                        continue;

                    return Exceptions.DependencyNotFound(migrations[i].Id, reference);
                }

                if (distinctNamesByKey[key].Count > 1)
                    return Exceptions.DependencyAmbiguous(migrations[i].Id, reference);

                var addedEdge = false;
                foreach (var target in targets)
                {
                    if (inContext[target] is false || target == i)
                        continue;

                    if (edges.Add((target, i)))
                    {
                        adjacency[target].Add(i);
                        predecessors[i].Add(target);
                        indegree[i]++;
                    }
                    addedEdge = true;
                }

                if (addedEdge is false && targets.Any(t => inContext[t]) is false)
                    return Exceptions.DependencyFilteredOut(migrations[i].Id, reference);
            }
        }

        var ready = new SortedSet<int>();
        for (var i = 0; i < count; i++)
        {
            if (indegree[i] == 0)
                ready.Add(i);
        }

        var ordered = new List<Migration>(count);
        while (ready.Count > 0)
        {
            var next = ready.Min;
            ready.Remove(next);
            ordered.Add(migrations[next]);

            foreach (var successor in adjacency[next])
            {
                if (--indegree[successor] == 0)
                    ready.Add(successor);
            }
        }

        if (ordered.Count < count)
            return Exceptions.DependencyCycle(DescribeCycle(migrations, predecessors, indegree));

        return ordered;
    }

    private static string DescribeCycle(IReadOnlyList<Migration> migrations, List<int>[] predecessors, int[] indegree)
    {
        var start = -1;
        for (var i = 0; i < migrations.Count; i++)
        {
            if (indegree[i] > 0)
            {
                start = i;
                break;
            }
        }

        var path = new List<int>();
        var seen = new HashSet<int>();
        var current = start;
        while (current != -1 && seen.Add(current))
        {
            path.Add(current);
            current = predecessors[current].FirstOrDefault(p => indegree[p] > 0, -1);
        }

        var cycleStart = current == -1 ? 0 : path.IndexOf(current);
        var cycle = path.Skip(cycleStart).Select(i => migrations[i].Id).ToList();
        if (current != -1)
            cycle.Add(migrations[current].Id);

        return string.Join("\n  -> ", cycle);
    }

    private static string Key(string path) => path.Replace('\\', '/').TrimStart('/').Trim().ToLowerInvariant();
}
