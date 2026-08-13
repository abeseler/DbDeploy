using Dapper;

namespace DbDeploy.Data;

internal sealed class Repository(IDatabaseProvider dbProvider, ILogger<Repository> logger)
{
    private MigrationLock? _migrationLock;
    public int MigrationsApplied { get; private set; } = 0;
    public int MigrationsSynced { get; private set; } = 0;

    public async Task EnsureMigrationTablesExist(CancellationToken stoppingToken = default)
    {
        using var connection = await dbProvider.ConnectAsync(stoppingToken);
        await connection.ExecuteAsync(dbProvider.EnsureMigrationTablesExist);
    }

    public async Task<bool> AcquireLock(TimeSpan maxWaitDuration, CancellationToken stoppingToken = default)
    {
        var waitUntil = DateTime.UtcNow.Add(maxWaitDuration);
        while (DateTime.UtcNow < waitUntil)
        {
            stoppingToken.ThrowIfCancellationRequested();
            {
                using var connection = await dbProvider.ConnectAsync(stoppingToken);
                _migrationLock = await connection.QuerySingleOrDefaultAsync<MigrationLock>(dbProvider.AcquireLock);
            }
            if (_migrationLock is not null)
            {
                logger.LogInformation("Lock acquired. DeploymentId: {DeploymentId}", _migrationLock.DeploymentId);
                break;
            }

            logger.LogWarning("Failed to acquire lock. Will retry for another {AcquireLockWait} seconds", Math.Ceiling((waitUntil - DateTime.UtcNow).TotalSeconds));
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        return _migrationLock is not null;
    }

    public async Task ReleaseLock(CancellationToken stoppingToken = default)
    {
        if (_migrationLock is null) return;

        using var connection = await dbProvider.ConnectAsync(stoppingToken);
        await connection.ExecuteAsync(dbProvider.ReleaseLock, _migrationLock);

        logger.LogDebug("Lock released. DeploymentId: {DeploymentId}", _migrationLock.DeploymentId);
        _migrationLock = null;
    }

    public async Task<Dictionary<string, MigrationHistory>> GetAllMigrationHistories(CancellationToken stoppingToken = default)
    {
        using var connection = await dbProvider.ConnectAsync(stoppingToken);
        var migrationHistories = await connection.QueryAsync<MigrationHistory>(dbProvider.GetAllMigrationHistories);

        return migrationHistories.ToDictionary(x => x.MigrationId, x => x);
    }

    public async Task<Result<Success>> ApplyMigration(Migration migration, MigrationHistory? migrationHistory, CancellationToken stoppingToken = default)
    {
        var hasExistingHistoryRecord = migrationHistory is not null;
        migrationHistory ??= new()
        {
            FileName = migration.FileName,
            Title = migration.Title!
        };

        migrationHistory.Hash = migration.Hash;
        migrationHistory.ExecutedOn = DateTime.UtcNow;
        migrationHistory.ExecutedSequence = MigrationsApplied + 1;
        migrationHistory.DeploymentId = _migrationLock?.DeploymentId;

        using var connection = await dbProvider.ConnectAsync(stoppingToken);
        using var transaction = migration.RunInTransaction ? connection.BeginTransaction() : null;
        try
        {
            foreach (var sql in migration.SqlStatements!)
            {
                stoppingToken.ThrowIfCancellationRequested();
                await connection.ExecuteAsync(sql, transaction: transaction, commandTimeout: migration.Timeout);
            }
            await connection.ExecuteAsync(hasExistingHistoryRecord ? dbProvider.UpdateMigrationHistory : dbProvider.InsertMigrationHistory, migrationHistory, transaction: transaction);
            transaction?.Commit();

            MigrationsApplied++;
            return Success.Default;
        }
        catch (Exception ex)
        {
            transaction?.Rollback();

            if (stoppingToken.IsCancellationRequested) throw;

            logger.LogError("Migration failed: {MigrationId}\n\n{ErrorMessage}\n", migration.Id, ex.Message);

            if (migration.OnError == Migration.ErrorHandling.Mark)
            {
                logger.LogWarning("Marking complete because OnError is '{OnError}'", migration.OnError);
                await connection.ExecuteAsync(hasExistingHistoryRecord ? dbProvider.UpdateMigrationHistory : dbProvider.InsertMigrationHistory, migrationHistory);
            }

            return migration.OnError == Migration.ErrorHandling.Fail ? new Exception(ex.Message) : Success.Default;
        }
    }

    public async Task SyncMigrationHistory(Migration migration, MigrationHistory? migrationHistory, CancellationToken stoppingToken = default)
    {
        var hasExistingHistoryRecord = migrationHistory is not null;
        migrationHistory ??= new()
        {
            FileName = migration.FileName,
            Title = migration.Title!
        };

        migrationHistory.Hash = migration.Hash;
        migrationHistory.ExecutedSequence = null;
        migrationHistory.DeploymentId = _migrationLock?.DeploymentId;

        using var connection = await dbProvider.ConnectAsync(stoppingToken);
        await connection.ExecuteAsync(hasExistingHistoryRecord ? dbProvider.UpdateMigrationHistory : dbProvider.InsertMigrationHistory, migrationHistory);

        MigrationsSynced++;
    }
}
