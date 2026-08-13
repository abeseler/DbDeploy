using DbDeploy.FileHandling;

namespace DbDeploy.Commands;

internal sealed class StatusCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<UpdateCommand> logger) : ICommand
{
    public string Name => "status";
    private int MigrationsToSync = 0;
    private int MigrationsToApply = 0;
    private int MigrationsFilteredOut = 0;

    public async Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var migrationHistories = await repo.GetAllMigrationHistories(stoppingToken);
        var (migrations, extractionError) = extractor.ExtractFromStartingFile([.. migrationHistories.Values.Select(x => x.FileName)], stoppingToken);

        if (extractionError is not null)
            return extractionError;

        var contexts = settings.Value.Contexts?.Split(',').Select(x => x.Trim()).ToArray() ?? [];
        foreach (var migration in migrations!.Values)
        {
            if (migration.IsMissingRequiredContext(contexts))
            {
                MigrationsFilteredOut++;
                continue;
            }
            if (migrationHistories.TryGetValue(migration.Id, out var migrationHistory) && migrationHistory is { Hash: null })
            {
                MigrationsToSync++;
                continue;
            }

            if (migration.HasInvalidChange(migrationHistory))
                logger.LogWarning("Validation error: {ErrorMessage}", Exceptions.MigrationHasInvalidChange(migration.Id).Message);

            if (migrationHistory is null || migration.RunAlways || (migration.RunOnChange && migrationHistory.Hash != migration.Hash))
            {
                MigrationsToApply++;
            }
        }

        logger.LogInformation("""
            Deployment Results:

                Pending Apply       =  {Applied}
                Previously applied  =  {PreviouslyApplied}
                Pending Sync        =  {Synced}
                Filtered out        =  {FilteredOut}

            """, MigrationsToApply, migrationHistories.Count, MigrationsToSync, MigrationsFilteredOut);

        return Success.Default;
    }
}
