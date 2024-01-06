namespace DbDeploy.Commands;

internal sealed class UpdateCommandHandler(MigrationRepository repository, IOptions<DeploymentOptions> options, ILogger<UpdateCommandHandler> logger)
{
    private readonly ILogger<UpdateCommandHandler> _logger = logger;
    private readonly IOptions<DeploymentOptions> _options = options;
    private readonly MigrationRepository _repository = repository;

    public async Task<Result<Success, Exception>> ExecuteAsync(MigrationCollection migrations)
    {
        await _repository.EnsureMigrationTablesExist();

        try
        {
            var lockAquired = await _repository.AquireLock(TimeSpan.FromSeconds(_options.Value.MaxLockWaitSeconds));

            if (lockAquired is false)
            {
                _logger.LogError("Could not aquire migration lock");
                return new InvalidOperationException("Could not aquire migration lock");
            }

            var migrationHistories = await _repository.GetExecutedMigrations();
            var contexts = _options.Value.Contexts?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
            var migrationsSkipped = 0;

            foreach (var migration in migrations.Values)
            {
                if (migration.IsMissingRequiredContext(contexts))
                {
                    _logger.LogTrace("Skipping migration: {FileName} [{Title}]", migration.FileName, migration.Title);
                    migrationsSkipped++;
                    continue;
                }

                migrationHistories.TryGetValue(migration.GetKey(), out var migrationHistory);

                if (migrationHistory is { Hash: null })
                {
                    _logger.LogTrace("Syncing migration: {FileName} [{Title}]", migration.FileName, migration.Title);
                    await _repository.SyncMigrationHistory(migration, migrationHistory);
                    continue;
                }

                if (migrationHistory is null || migration.RunAlways || (migration.RunOnChange && migrationHistory?.Hash != migration.Hash))
                {
                    _logger.LogTrace("Applying migration: {FileName} [{Title}]", migration.FileName, migration.Title);
                    var result = await _repository.ApplyMigration(migration, migrationHistory);
                    var continueNextMigration = result.Match(
                        onSuccess: success => true,
                        onFailure: error =>
                        {
                            _logger.LogError("Migration failed: {FileName} [{Title}]: {ErrorMessage}", migration.FileName, migration.Title, error.Message);
                            return false;
                        });

                    if (continueNextMigration is false && migration.OnError == Migration.ErrorHandling.Fail)
                        return new Exception();

                    _logger.LogInformation("Migration applied: {FileName} [{Title}]", migration.FileName, migration.Title);
                }
            }

            _logger.LogInformation("Migrations applied: {AppliedCount}", _repository.MigrationsApplied);
            _logger.LogInformation("Migrations  synced: {SyncedCount}", _repository.MigrationsSynced);
            _logger.LogInformation("Migrations skipped: {SkippedCount}", migrationsSkipped);

            return Success.Default;
        }
        finally
        {
            await _repository.ReleaseLock();
        }
    }
}
