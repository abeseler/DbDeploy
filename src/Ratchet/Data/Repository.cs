using System.Data;
using Dapper;

namespace Ratchet.Data;

internal sealed class Repository(IDatabaseProvider dbProvider, ILogger<Repository> logger)
{
    private MigrationLock? _migrationLock;
    private IDbConnection? _lockConnection;
    private int _lastExecutedSequence;
    public int MigrationsApplied { get; private set; } = 0;
    public int MigrationsSynced { get; private set; } = 0;
    public int MigrationsSkipped { get; private set; } = 0;
    public int MigrationsMarked { get; private set; } = 0;

    private async Task<(IDbConnection Connection, bool Dispose)> Lease(CancellationToken stoppingToken) =>
        _lockConnection is { } held ? (held, false) : (await dbProvider.ConnectAsync(stoppingToken), true);

    public async Task EnsureMigrationTablesExist(CancellationToken stoppingToken = default)
    {
        using var connection = await dbProvider.ConnectAsync(stoppingToken);
        await connection.ExecuteAsync(dbProvider.EnsureMigrationTablesExist);
    }

    public async Task<bool> AcquireLock(TimeSpan maxWaitDuration, CancellationToken stoppingToken = default)
    {
        var connection = await dbProvider.ConnectAsync(stoppingToken);
        if (await dbProvider.TryAcquireSessionLock(connection, maxWaitDuration, stoppingToken) is false)
        {
            logger.LogWarning("Failed to acquire lock within {MaxWaitSeconds} seconds", Math.Ceiling(maxWaitDuration.TotalSeconds));
            connection.Dispose();
            return false;
        }

        _lockConnection = connection;
        _migrationLock = await connection.QuerySingleAsync<MigrationLock>(dbProvider.AcquireLock);
        logger.LogInformation("Lock acquired. DeploymentId: {DeploymentId}", _migrationLock.DeploymentId);
        return true;
    }

    public async Task ReleaseLock(CancellationToken stoppingToken = default)
    {
        if (_lockConnection is null) return;

        if (_migrationLock is not null)
        {
            await _lockConnection.ExecuteAsync(dbProvider.ReleaseLock, _migrationLock);
            logger.LogDebug("Lock released. DeploymentId: {DeploymentId}", _migrationLock.DeploymentId);
        }

        await dbProvider.ReleaseSessionLock(_lockConnection, stoppingToken);
        _lockConnection.Dispose();
        _lockConnection = null;
        _migrationLock = null;
    }

    public async Task<Dictionary<string, MigrationHistory>> GetAllMigrationHistories(CancellationToken stoppingToken = default)
    {
        var (connection, dispose) = await Lease(stoppingToken);
        try
        {
            var migrationHistories = (await connection.QueryAsync<MigrationHistory>(dbProvider.GetAllMigrationHistories)).ToList();
            _lastExecutedSequence = migrationHistories
                .Select(h => h.ExecutedSequence ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            return migrationHistories.ToDictionary(x => x.MigrationId, x => x);
        }
        finally
        {
            if (dispose) connection.Dispose();
        }
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
        migrationHistory.DeploymentId = _migrationLock?.DeploymentId;

        var (connection, dispose) = await Lease(stoppingToken);
        var transaction = migration.RunInTransaction ? connection.BeginTransaction() : null;
        try
        {
            foreach (var sql in migration.SqlStatements!)
            {
                stoppingToken.ThrowIfCancellationRequested();
                await connection.ExecuteAsync(sql, transaction: transaction, commandTimeout: migration.Timeout);
            }
            AssignExecutedSequenceIfNeeded(migrationHistory);
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
                AssignExecutedSequenceIfNeeded(migrationHistory);
                await connection.ExecuteAsync(hasExistingHistoryRecord ? dbProvider.UpdateMigrationHistory : dbProvider.InsertMigrationHistory, migrationHistory);
                MigrationsMarked++;
            }
            else if (migration.OnError == Migration.ErrorHandling.Skip)
            {
                MigrationsSkipped++;
            }

            return migration.OnError == Migration.ErrorHandling.Fail ? new Exception(ex.Message) : Success.Default;
        }
        finally
        {
            transaction?.Dispose();
            if (dispose) connection.Dispose();
        }
    }

    private void AssignExecutedSequenceIfNeeded(MigrationHistory history)
    {
        if (history.ExecutedSequence is not null)
            return;

        history.ExecutedSequence = ++_lastExecutedSequence;
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

        var (connection, dispose) = await Lease(stoppingToken);
        try
        {
            await connection.ExecuteAsync(hasExistingHistoryRecord ? dbProvider.UpdateMigrationHistory : dbProvider.InsertMigrationHistory, migrationHistory);
            MigrationsSynced++;
        }
        finally
        {
            if (dispose) connection.Dispose();
        }
    }
}
