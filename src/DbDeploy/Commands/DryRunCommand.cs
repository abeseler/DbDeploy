using DbDeploy.FileHandling;

namespace DbDeploy.Commands;

internal sealed class DryRunCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<DryRunCommand> logger) : ICommand
{
    private const string DefaultOutputFile = "dbdeploy-plan.sql";
    public string Name => "dryrun";

    public async Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var migrationHistories = await repo.GetAllMigrationHistories(stoppingToken);
        var (migrations, extractionError) = extractor.ExtractFromStartingFile([.. migrationHistories.Values.Select(x => x.FileName)], stoppingToken);

        if (extractionError is not null)
            return extractionError;

        var contexts = settings.Value.Contexts?.Split(',').Select(x => x.Trim()).ToArray() ?? [];
        var plan = new List<Migration>();
        var resolvedInContext = new List<Migration>();
        var migrationsToSync = 0;
        var migrationsFilteredOut = 0;

        foreach (var migration in migrations!.Values)
        {
            if (migration.IsMissingRequiredContext(contexts))
            {
                migrationsFilteredOut++;
                continue;
            }
            resolvedInContext.Add(migration);
            if (migrationHistories.TryGetValue(migration.Id, out var migrationHistory) && migrationHistory is { Hash: null })
            {
                migrationsToSync++;
                continue;
            }

            if (migration.HasInvalidChange(migrationHistory))
                logger.LogWarning("Validation error: {ErrorMessage}", Exceptions.MigrationHasInvalidChange(migration.Id).Message);

            if (migrationHistory is null || migration.RunAlways || (migration.RunOnChange && migrationHistory.Hash != migration.Hash))
                plan.Add(migration);
        }

        var outputPath = ResolveOutputPath();
        await WritePlan(outputPath, plan, resolvedInContext, migrationHistories, stoppingToken);

        logger.LogInformation("""
            Deployment Results:

                Pending Apply       =  {Applied}
                Previously applied  =  {PreviouslyApplied}
                Pending Sync        =  {Synced}
                Filtered out        =  {FilteredOut}

                Plan written to     =  {OutputFile}

            """, plan.Count, migrationHistories.Count, migrationsToSync, migrationsFilteredOut, outputPath);

        return Success.Default;
    }

    private string ResolveOutputPath()
    {
        var file = string.IsNullOrWhiteSpace(settings.Value.OutputFile) ? DefaultOutputFile : settings.Value.OutputFile;
        return Path.GetFullPath(file, AppDomain.CurrentDomain.BaseDirectory);
    }

    private async Task WritePlan(string path, IReadOnlyList<Migration> plan, IReadOnlyList<Migration> resolvedInContext, IReadOnlyDictionary<string, MigrationHistory> histories, CancellationToken stoppingToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) is false)
            Directory.CreateDirectory(directory);

        await using var writer = new StreamWriter(path, append: false);
        await writer.WriteLineAsync($"-- DbDeploy plan generated {DateTimeOffset.UtcNow:u}");
        await writer.WriteLineAsync($"-- Provider: {settings.Value.DatabaseProvider}");
        await writer.WriteLineAsync($"-- Migrations to apply: {plan.Count}");
        await writer.WriteLineAsync();

        foreach (var migration in plan)
        {
            stoppingToken.ThrowIfCancellationRequested();
            await WriteMigration(writer, migration);
        }

        await WriteReorderFooter(writer, resolvedInContext, histories);
    }

    // Compares the resolved relative order of already-applied migrations against the order they
    // were applied in the target database (ExecutedSequence). An inversion means a dependency or
    // folder change moved a migration relative to something already applied — harmless on this
    // database, but a signal it could fail on a fresh one unless the ordering is made explicit.
    private static async Task WriteReorderFooter(StreamWriter writer, IReadOnlyList<Migration> resolvedInContext, IReadOnlyDictionary<string, MigrationHistory> histories)
    {
        var applied = resolvedInContext
            .Select((migration, resolvedIndex) => (migration, resolvedIndex, history: histories.GetValueOrDefault(migration.Id)))
            .Where(x => x.history is { ExecutedSequence: not null })
            .OrderBy(x => x.resolvedIndex)
            .ToList();

        var inversions = new List<string>();
        for (var i = 0; i < applied.Count; i++)
        {
            for (var j = i + 1; j < applied.Count; j++)
            {
                if (applied[i].history!.ExecutedSequence > applied[j].history!.ExecutedSequence)
                    inversions.Add($"{applied[j].migration.Id}  now applies AFTER  {applied[i].migration.Id}  (previously before it)");
            }
        }

        await writer.WriteLineAsync("-- ============================================================");
        await writer.WriteLineAsync("-- Reorderings relative to applied history (informational)");
        await writer.WriteLineAsync($"-- Target database applied {applied.Count} of these migrations previously.");
        if (inversions.Count == 0)
        {
            await writer.WriteLineAsync("-- No reorderings relative to applied history.");
            await writer.WriteLineAsync("-- ============================================================");
            return;
        }

        await writer.WriteLineAsync($"-- {inversions.Count} pair(s) now resolve in a different relative order:");
        await writer.WriteLineAsync("-- ------------------------------------------------------------");
        foreach (var inversion in inversions)
            await writer.WriteLineAsync($"--   {inversion}");
        await writer.WriteLineAsync("-- Declare a dependsOn if the new order is required on a fresh database.");
        await writer.WriteLineAsync("-- ============================================================");
    }

    // Transaction boundaries are emitted as comments rather than BEGIN/COMMIT because the executor
    // wraps each migration in an ADO.NET transaction; no such SQL is actually sent to the database.
    private static async Task WriteMigration(StreamWriter writer, Migration migration)
    {
        await writer.WriteLineAsync("-- ============================================================");
        await writer.WriteLineAsync($"-- Migration: {migration.Id}");
        await writer.WriteLineAsync($"-- transaction={(migration.RunInTransaction ? "true" : "false")}  timeout={migration.Timeout}  onError={migration.OnError}");
        if (migration.RunAlways)
            await writer.WriteLineAsync("-- runAlways=true");
        if (migration.RunOnChange)
            await writer.WriteLineAsync("-- runOnChange=true");
        await writer.WriteLineAsync("-- ============================================================");

        if (migration.RunInTransaction)
            await writer.WriteLineAsync("-- transaction begin");

        foreach (var sql in migration.SqlStatements)
        {
            await writer.WriteLineAsync(sql.Trim());
            await writer.WriteLineAsync();
        }

        if (migration.RunInTransaction)
            await writer.WriteLineAsync("-- transaction commit");

        await writer.WriteLineAsync();
    }
}
