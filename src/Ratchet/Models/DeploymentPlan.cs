namespace Ratchet.Models;

internal sealed class DeploymentPlan
{
    public required IReadOnlyList<Migration> Resolved { get; init; }
    public required IReadOnlyList<PlannedMigration> ToApply { get; init; }
    public required IReadOnlyList<PlannedMigration> ToBaseline { get; init; }
    public required IReadOnlyList<PlannedMigration> PendingBaseline { get; init; }
    public required IReadOnlyList<PlannedMigration> ToRepair { get; init; }
    public required IReadOnlyList<Migration> FilteredOut { get; init; }
    public required IReadOnlyDictionary<string, MigrationHistory> Histories { get; init; }

    public int HistoryCount => Histories.Count;
    public int UpToDateCount => Resolved.Count - ToApply.Count - PendingBaseline.Count - ToRepair.Count;
}

internal readonly record struct PlannedMigration(Migration Migration, MigrationHistory? History);

internal static class DeploymentPlanner
{
    public static Result<DeploymentPlan> Prepare(
        IReadOnlyList<Migration> parsed,
        IReadOnlyDictionary<string, MigrationHistory> histories,
        string[] contexts)
    {
        var applied = histories.Values.Select(h => new AppliedMigration(h.FileName, h.Title)).ToList();
        var (ordered, error) = MigrationOrderResolver.Resolve(parsed, contexts, applied);
        if (error is not null)
            return error;

        return Build(ordered!, histories, contexts);
    }

    public static DeploymentPlan Build(
        IEnumerable<Migration> migrations,
        IReadOnlyDictionary<string, MigrationHistory> histories,
        string[] contexts)
    {
        var resolved = new List<Migration>();
        var toApply = new List<PlannedMigration>();
        var toBaseline = new List<PlannedMigration>();
        var toRepair = new List<PlannedMigration>();
        var filteredOut = new List<Migration>();

        foreach (var migration in migrations)
        {
            if (migration.IsMissingRequiredContext(contexts))
            {
                filteredOut.Add(migration);
                continue;
            }

            resolved.Add(migration);
            histories.TryGetValue(migration.Id, out var history);

            if (history is { Hash: null })
            {
                toBaseline.Add(new(migration, history));
                continue;
            }

            if (history is null)
                toBaseline.Add(new(migration, history));

            if (migration.HasInvalidChange(history))
                toRepair.Add(new(migration, history));

            if (history is null || migration.RunAlways || (migration.RunOnChange && history.Hash != migration.Hash))
                toApply.Add(new(migration, history));
        }

        var applyIds = toApply.Select(p => p.Migration.Id).ToHashSet(StringComparer.Ordinal);
        var pendingBaseline = toBaseline.Where(b => applyIds.Contains(b.Migration.Id) is false).ToList();

        return new DeploymentPlan
        {
            Resolved = resolved,
            ToApply = toApply,
            ToBaseline = toBaseline,
            PendingBaseline = pendingBaseline,
            ToRepair = toRepair,
            FilteredOut = filteredOut,
            Histories = histories
        };
    }
}
