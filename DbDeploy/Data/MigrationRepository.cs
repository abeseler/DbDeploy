using Dapper;

namespace DbDeploy.Data;

internal sealed class MigrationRepository(DbConnector dbConnector, ILogger<MigrationRepository> logger)
{
    private readonly ILogger<MigrationRepository> _logger = logger;
    private readonly DbConnector _dbConnector = dbConnector;
    private MigrationLock? _migrationLock;
    public int MigrationsApplied { get; private set; } = 0;
    public int MigrationsSynced { get; private set; } = 0;

    public async Task EnsureMigrationTablesExist()
    {
        using var connection = await _dbConnector.ConnectAsync();
        await connection.ExecuteAsync(SqlStatements.EnsureMigrationTablesExist);
    }

    public async Task<bool> AquireLock(TimeSpan maxWaitDuration)
    {
        var waitUntil = DateTime.UtcNow.Add(maxWaitDuration);
        while (DateTime.UtcNow < waitUntil)
        {
            {
                using var connection = await _dbConnector.ConnectAsync();
                _migrationLock = await connection.QuerySingleOrDefaultAsync<MigrationLock>(SqlStatements.AquireLock);
            }
            if (_migrationLock is not null)
            {
                _logger.LogInformation("Lock aquired. DeploymentId: {DeploymentId}", _migrationLock.DeploymentId);
                break;
            }

            _logger.LogWarning("Failed to aquire lock. Will retry for another {AquireLockWait} seconds", Math.Ceiling((waitUntil - DateTime.UtcNow).TotalSeconds));
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        return _migrationLock is not null;
    }

    public async Task ReleaseLock()
    {
        if (_migrationLock is null) return;

        _migrationLock.MigrationsApplied = MigrationsApplied;
        using var connection = await _dbConnector.ConnectAsync();
        await connection.ExecuteAsync(SqlStatements.ReleaseLock, _migrationLock);

        _logger.LogDebug("Lock released. DeploymentId: {DeploymentId}", _migrationLock.DeploymentId);
        _migrationLock = null;
    }

    public async Task<Dictionary<string, MigrationHistory>> GetExecutedMigrations()
    {
        using var connection = await _dbConnector.ConnectAsync();
        var migrationHistories = await connection.QueryAsync<MigrationHistory>(SqlStatements.GetExecutedMigrations);

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

        using var connection = await _dbConnector.ConnectAsync();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var sql in migration.SqlStatements!)
            {
                await connection.ExecuteAsync(sql, transaction: transaction, commandTimeout: migration.Timeout ?? 30);
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

        using var connection = await _dbConnector.ConnectAsync();
        await connection.ExecuteAsync(hasExistingHistoryRecord ? SqlStatements.UpdateMigrationHistory : SqlStatements.InsertMigrationHistory, migrationHistory);

        MigrationsSynced++;
    }
}
