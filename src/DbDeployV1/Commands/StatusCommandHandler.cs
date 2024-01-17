using DbDeployV1.Data;

namespace DbDeployV1.Commands;

internal sealed class StatusCommandHandler(MigrationRepository repository, IOptions<DeploymentOptions> options, ILogger<UpdateCommandHandler> logger)
{
    private readonly ILogger<UpdateCommandHandler> _logger = logger;
    private readonly IOptions<DeploymentOptions> _options = options;
    private readonly MigrationRepository _repository = repository;

    public async Task<Result<Success, Exception>> ExecuteAsync(MigrationCollection migrations)
    {
        await _repository.EnsureMigrationTablesExist();

        var migrationHistories = await _repository.GetExecutedMigrations();
        var contexts = _options.Value.Contexts?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        var migrationsToSkip = 0;
        var migrationsToSync = 0;
        var migrationsToApply = 0;

        foreach (var migration in migrations.Values)
        {
            if (migration.IsMissingRequiredContext(contexts))
            {
                _logger.LogInformation("Migration will be skipped: {FileName} [{Title}]", migration.FileName, migration.Title);
                migrationsToSkip++;
                continue;
            }

            migrationHistories.TryGetValue(migration.GetKey(), out var migrationHistory);

            if (migrationHistory is { Hash: null })
            {
                _logger.LogInformation("Migration will be  synced: {FileName} [{Title}]", migration.FileName, migration.Title);
                migrationsToSync++;
                continue;
            }

            if (migrationHistory is null || migration.RunAlways || migration.RunOnChange && migrationHistory?.Hash != migration.Hash)
            {
                _logger.LogInformation("Migration will be applied: {FileName} [{Title}]", migration.FileName, migration.Title);
                migrationsToApply++;
            }
        }

        _logger.LogInformation("Number of migrations to apply: {AppliedCount}", migrationsToApply);
        _logger.LogInformation("Number of migrations  to sync: {SyncedCount}", migrationsToSync);
        _logger.LogInformation("Number of migrations  to skip: {SkippedCount}", migrationsToSkip);

        return Success.Default;
    }
}
