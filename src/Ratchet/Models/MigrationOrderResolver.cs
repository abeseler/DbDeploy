namespace Ratchet.Models;

internal static class MigrationOrderResolver
{
    public static Result<List<Migration>> Resolve(IReadOnlyList<Migration> migrations, string[] contexts, IEnumerable<MigrationHistory> applied)
    {
        var appliedFileKeys = applied.Select(a => Key(a.FileName)).ToHashSet();
        var appliedBlockKeys = applied.Select(a => BlockKey(a.FileName, a.Title)).ToHashSet();
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
                var (file, title) = ParseReference(reference);
                if (file.Length == 0 || title is { Length: 0 })
                    return Errors.DependencyNotFound(migrations[i].Id, reference);

                var key = Key(file);
                if (filesByKey.TryGetValue(key, out var fileTargets) is false)
                {
                    if (IsApplied(file, title, appliedFileKeys, appliedBlockKeys))
                        continue;

                    return Errors.DependencyNotFound(migrations[i].Id, reference);
                }

                if (distinctNamesByKey[key].Count > 1)
                    return Errors.DependencyAmbiguous(migrations[i].Id, reference);

                var targets = fileTargets;
                if (title is not null)
                {
                    var titleMatches = fileTargets
                        .Where(t => migrations[t].Title.Equals(title, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (titleMatches.Count == 0)
                    {
                        if (IsApplied(file, title, appliedFileKeys, appliedBlockKeys))
                            continue;

                        return Errors.DependencyNotFound(migrations[i].Id, reference);
                    }

                    if (titleMatches.Count > 1)
                        return Errors.DependencyAmbiguous(migrations[i].Id, reference);

                    targets = titleMatches;
                }

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
                    return Errors.DependencyFilteredOut(migrations[i].Id, reference);
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
            return Errors.DependencyCycle(DescribeCycle(migrations, predecessors, indegree));

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

    private static (string File, string? Title) ParseReference(string reference)
    {
        var trimmed = reference.Trim();
        var hash = trimmed.IndexOf('#');
        if (hash < 0)
            return (trimmed, null);

        return (trimmed[..hash].Trim(), trimmed[(hash + 1)..].Trim());
    }

    private static bool IsApplied(string file, string? title, HashSet<string> appliedFileKeys, HashSet<string> appliedBlockKeys) =>
        title is null ? appliedFileKeys.Contains(Key(file)) : appliedBlockKeys.Contains(BlockKey(file, title));

    private static string BlockKey(string file, string title) => $"{Key(file)}#{title.Trim().ToLowerInvariant()}";

    private static string Key(string path) => path.Replace('\\', '/').TrimStart('/').Trim().ToLowerInvariant();
}
