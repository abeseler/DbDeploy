﻿using DbDeploy.FileHandling;

namespace DbDeploy.Commands;

internal sealed class UpdateCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<UpdateCommand> logger) : ICommand
{
    public string Name => "update";
    private readonly List<(Migration, MigrationHistory?)> MigrationsToSync = [];
    private readonly List<(Migration, MigrationHistory?)> MigrationsToApply = [];
    private int MigrationsFilteredOut = 0;

    public async Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var (migrations, parsingErrors) = extractor.ExtractFromStartingFile(stoppingToken);

        if (parsingErrors > 0)
            return Exceptions.MigrationsParsingError(parsingErrors);

        try
        {
            if (await repo.AcquireLock(TimeSpan.FromSeconds(settings.Value.LockWaitMaxSeconds), stoppingToken) is false)
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
                if (migrationHistories.TryGetValue(migration.Id, out var migrationHistory) && migrationHistory is { Hash: null })
                {
                    MigrationsToSync.Add((migration, migrationHistory));
                    continue;
                }

                if (migration.HasInvalidChange(migrationHistory))
                    return Exceptions.MigrationHasInvalidChange(migration.Id);

                if (migrationHistory is null || migration.RunAlways || (migration.RunOnChange && migrationHistory.Hash != migration.Hash))
                {
                    MigrationsToApply.Add((migration, migrationHistory));
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

                    """, MigrationsToApply.Count, migrationHistories.Count, MigrationsToSync.Count, MigrationsFilteredOut);

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

        var migrationsApplied = 0;
        foreach (var (migration, history) in MigrationsToApply)
        {
            stoppingToken.ThrowIfCancellationRequested();
            logger.LogInformation("Applying migration: {MigrationId}", migration.Id);
            var result = await repo.ApplyMigration(migration, history, stoppingToken);
            var continueToNextMigration = result.Match(
                onSuccess: _ => true,
                onFailure: error => false);

            if (continueToNextMigration is false)
                return Exceptions.DeploymentFailed(MigrationsToApply.Count - migrationsApplied);

            migrationsApplied++;
        }

        return Success.Default;
    }
}
