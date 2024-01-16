using Dapper;
using DbDeploy.Data;

namespace DbDeploy.Migrations;

internal sealed class Repository(DbConnector dbConnector, ILogger<Repository> logger)
{
    private MigrationLock? _migrationLock;
    public int MigrationsApplied { get; private set; } = 0;
    public int MigrationsSynced { get; private set; } = 0;

    public async Task EnsureMigrationTablesExist()
    {
        using var connection = await dbConnector.ConnectAsync();
        await connection.ExecuteAsync(SqlStatements.EnsureMigrationTablesExist);
    }

    public async Task<bool> AcquireLock(TimeSpan maxWaitDuration)
    {
        var waitUntil = DateTime.UtcNow.Add(maxWaitDuration);
        while (DateTime.UtcNow < waitUntil)
        {
            {
                using var connection = await dbConnector.ConnectAsync();
                _migrationLock = await connection.QuerySingleOrDefaultAsync<MigrationLock>(SqlStatements.AcquireLock);
            }
            if (_migrationLock is not null)
            {
                logger.LogInformation("Lock acquired. DeploymentId: {DeploymentId}", _migrationLock.DeploymentId);
                break;
            }

            logger.LogWarning("Failed to acquire lock. Will retry for another {AcquireLockWait} seconds", Math.Ceiling((waitUntil - DateTime.UtcNow).TotalSeconds));
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        return _migrationLock is not null;
    }

    public async Task ReleaseLock()
    {
        if (_migrationLock is null) return;

        using var connection = await dbConnector.ConnectAsync();
        await connection.ExecuteAsync(SqlStatements.ReleaseLock, _migrationLock);

        logger.LogDebug("Lock released. DeploymentId: {DeploymentId}", _migrationLock.DeploymentId);
        _migrationLock = null;
    }

    public async Task<Dictionary<string, MigrationHistory>> GetAllMigrationHistories()
    {
        using var connection = await dbConnector.ConnectAsync();
        var migrationHistories = await connection.QueryAsync<MigrationHistory>(SqlStatements.GetAllMigrationHistories);

        return migrationHistories.ToDictionary(x => x.GetKey(), x => x);
    }

    public async Task<Result<Success, Error>> ApplyMigration(Migration migration, MigrationHistory? migrationHistory)
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

        using var connection = await dbConnector.ConnectAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var sql in migration.SqlStatements!)
            {
                await connection.ExecuteAsync(sql, transaction: transaction, commandTimeout: migration.Timeout);
            }
            await connection.ExecuteAsync(hasExistingHistoryRecord ? SqlStatements.UpdateMigrationHistory : SqlStatements.InsertMigrationHistory, migrationHistory, transaction: transaction);
            transaction.Commit();

            MigrationsApplied++;
            return Success.Default;
        }
        catch (Exception ex)
        {
            transaction.Rollback();

            if (migration.OnError == Migration.ErrorHandling.Mark)
            {
                await connection.ExecuteAsync(hasExistingHistoryRecord ? SqlStatements.UpdateMigrationHistory : SqlStatements.InsertMigrationHistory, migrationHistory);
            }
            return migration.OnError == Migration.ErrorHandling.Fail ? new Error(ex.Message) : Success.Default;
        }
    }

    public async Task SyncMigrationHistory(Migration migration, MigrationHistory? migrationHistory)
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

        using var connection = await dbConnector.ConnectAsync();
        await connection.ExecuteAsync(hasExistingHistoryRecord ? SqlStatements.UpdateMigrationHistory : SqlStatements.InsertMigrationHistory, migrationHistory);

        MigrationsSynced++;
    }
}
