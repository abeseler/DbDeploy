using DbDeploy.FileHandling;

namespace DbDeploy.Commands;

internal sealed class DryRunCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<DryRunCommand> logger) : ICommand
{
    private const string DefaultOutputFile = "dbdeploy-plan.sql";
    public string Name => "dryrun";

    public async Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var (migrations, parsingErrors) = extractor.ExtractFromStartingFile(stoppingToken);

        if (parsingErrors > 0)
            return Exceptions.MigrationsParsingError(parsingErrors);

        var migrationHistories = await repo.GetAllMigrationHistories(stoppingToken);

        var contexts = settings.Value.Contexts?.Split(',').Select(x => x.Trim()).ToArray() ?? [];
        var plan = new List<Migration>();
        var migrationsToSync = 0;
        var migrationsFilteredOut = 0;

        foreach (var migration in migrations.Values)
        {
            if (migration.IsMissingRequiredContext(contexts))
            {
                migrationsFilteredOut++;
                continue;
            }
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
        await WritePlan(outputPath, plan, stoppingToken);

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

    private async Task WritePlan(string path, IReadOnlyList<Migration> plan, CancellationToken stoppingToken)
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
