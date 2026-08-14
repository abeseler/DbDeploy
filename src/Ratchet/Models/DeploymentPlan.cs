namespace Ratchet.Models;

internal sealed class DeploymentPlan
{
    public required IReadOnlyList<Migration> Resolved { get; init; }
    public required IReadOnlyList<PlannedMigration> ToApply { get; init; }
    public required IReadOnlyList<PlannedMigration> ToSync { get; init; }
    public required IReadOnlyList<Migration> FilteredOut { get; init; }
    public required IReadOnlyList<Migration> InvalidChanges { get; init; }
    public required IReadOnlyDictionary<string, MigrationHistory> Histories { get; init; }

    public int HistoryCount => Histories.Count;
}

internal readonly record struct PlannedMigration(Migration Migration, MigrationHistory? History);

internal static class DeploymentPlanner
{
    public static DeploymentPlan Build(
        IEnumerable<Migration> migrations,
        IReadOnlyDictionary<string, MigrationHistory> histories,
        string[] contexts)
    {
        var resolved = new List<Migration>();
        var toApply = new List<PlannedMigration>();
        var toSync = new List<PlannedMigration>();
        var filteredOut = new List<Migration>();
        var invalidChanges = new List<Migration>();

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
                toSync.Add(new(migration, history));
                continue;
            }

            if (migration.HasInvalidChange(history))
                invalidChanges.Add(migration);

            if (history is null || migration.RunAlways || (migration.RunOnChange && history.Hash != migration.Hash))
                toApply.Add(new(migration, history));
        }

        return new DeploymentPlan
        {
            Resolved = resolved,
            ToApply = toApply,
            ToSync = toSync,
            FilteredOut = filteredOut,
            InvalidChanges = invalidChanges,
            Histories = histories
        };
    }
}
