using DbDeploy.FileHandling;

namespace DbDeploy.Commands;

internal sealed class SyncCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<UpdateCommand> logger) : ICommand
{
    public string Name => "sync";
    private readonly List<(Migration, MigrationHistory?)> MigrationsToSync = [];
    private int MigrationsFilteredOut = 0;

    public async Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var (migrations, parsingErrors) = extractor.ExtractFromStartingFile(stoppingToken);

        if (parsingErrors > 0)
            return Exceptions.MigrationsParsingError(parsingErrors);

        try
        {
            if (await repo.AcquireLock(TimeSpan.FromSeconds(settings.Value.MaxLockWaitSeconds), stoppingToken) is false)
                return Exceptions.FailedToAcquireLock;

            var migrationHistories = await repo.GetAllMigrationHistories(stoppingToken);

            var contexts = settings.Value.Contexts?.Split(',').Select(x => x.Trim()).ToArray() ?? [];
            foreach (var migration in migrations.Values)
            {
                if (migration.IsMissingRequiredContext(contexts))
                {
                    MigrationsFilteredOut++;
                    continue;
                }
                migrationHistories.TryGetValue(migration.Id, out var migrationHistory);

                if (migrationHistory is null || migrationHistory.Hash != migration.Hash)
                {
                    MigrationsToSync.Add((migration, migrationHistory));
                }
            }

            var result = await ExecuteMigrations(stoppingToken);
            if (result.Succeeded)
                logger.LogInformation("""
                    Deployment Results:

                      Applied             =  {Applied}
                      Previously applied  =  {PreviouslyApplied}
                      Synced              =  {Synced}
                      Filtered out        =  {FilteredOut}

                    """, 0, migrationHistories.Count, MigrationsToSync.Count, MigrationsFilteredOut);

            return result;
        }
        finally
        {
            await repo.ReleaseLock(stoppingToken);
        }
    }

    public async Task<Result<Success>> ExecuteMigrations(CancellationToken stoppingToken = default)
    {
        foreach (var (migration, history) in MigrationsToSync)
        {
            stoppingToken.ThrowIfCancellationRequested();
            logger.LogInformation("Syncing migration: {MigrationId}", migration.Id);
            await repo.SyncMigrationHistory(migration, history, stoppingToken);
        }

        return Success.Default;
    }
}
