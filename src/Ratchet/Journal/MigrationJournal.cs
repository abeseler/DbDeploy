using System.Data;
using Dapper;

namespace Ratchet.Journal;

internal sealed class MigrationJournal(IDatabaseProvider dbProvider, ILogger<MigrationJournal> logger)
{
    private MigrationLock? _migrationLock;
    private IDbConnection? _lockConnection;
    private int _lastExecutedSequence;

    private async Task<(IDbConnection Connection, bool Dispose)> Lease(CancellationToken stoppingToken) =>
        _lockConnection is { } held ? (held, false) : (await dbProvider.ConnectAsync(stoppingToken), true);

    public async Task EnsureTables(CancellationToken stoppingToken = default)
    {
        using var connection = await dbProvider.ConnectAsync(stoppingToken);
        await connection.ExecuteAsync(dbProvider.EnsureTables);
    }

    public async Task<bool> AcquireLock(TimeSpan maxWaitDuration, CancellationToken stoppingToken = default)
    {
        logger.LogDebug("Waiting for lock (max {MaxWaitSeconds} seconds)", Math.Ceiling(maxWaitDuration.TotalSeconds));
        var connection = await dbProvider.ConnectAsync(stoppingToken);
        if (await dbProvider.TryAcquireSessionLock(connection, maxWaitDuration, stoppingToken) is false)
        {
            logger.LogWarning("Failed to acquire lock within {MaxWaitSeconds} seconds", Math.Ceiling(maxWaitDuration.TotalSeconds));
            connection.Dispose();
            return false;
        }

        _lockConnection = connection;
        _migrationLock = await connection.QuerySingleAsync<MigrationLock>(dbProvider.InsertLock);
        logger.LogInformation("Lock acquired. DeploymentId: {DeploymentId}", _migrationLock.DeploymentId);
        return true;
    }

    public async Task ReleaseLock(CancellationToken stoppingToken = default)
    {
        if (_lockConnection is null) return;

        if (_migrationLock is not null)
        {
            await _lockConnection.ExecuteAsync(dbProvider.FinishLock, _migrationLock);
            logger.LogDebug("Lock released. DeploymentId: {DeploymentId}", _migrationLock.DeploymentId);
        }

        await dbProvider.ReleaseSessionLock(_lockConnection, stoppingToken);
        _lockConnection.Dispose();
        _lockConnection = null;
        _migrationLock = null;
    }

    public async Task<Dictionary<string, MigrationHistory>> GetHistories(CancellationToken stoppingToken = default)
    {
        var (connection, dispose) = await Lease(stoppingToken);
        try
        {
            var migrationHistories = (await connection.QueryAsync<MigrationHistory>(dbProvider.SelectHistories)).ToList();
            _lastExecutedSequence = migrationHistories
                .Select(h => h.ExecutedSequence ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            return migrationHistories.ToDictionary(x => x.MigrationId, x => x, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (dispose) connection.Dispose();
        }
    }

    public async Task<Result<ApplyOutcome>> Apply(Migration migration, MigrationHistory? history, CancellationToken stoppingToken = default)
    {
        var update = history is not null;
        var (connection, dispose) = await Lease(stoppingToken);
        var transaction = migration.RunInTransaction ? connection.BeginTransaction() : null;
        try
        {
            foreach (var sql in migration.SqlStatements)
            {
                stoppingToken.ThrowIfCancellationRequested();
                await connection.ExecuteAsync(sql, transaction: transaction, commandTimeout: migration.Timeout);
            }

            await PersistHistory(connection, ApplyRow(migration, history), update, transaction);
            transaction?.Commit();
            return ApplyOutcome.Applied;
        }
        catch (Exception ex)
        {
            transaction?.Rollback();

            if (ex is OperationCanceledException || stoppingToken.IsCancellationRequested)
                throw;

            if (migration.OnError == Migration.ErrorHandling.Mark)
            {
                logger.LogWarning(ex, "Marking {MigrationId} as applied because onError is Mark", migration.Id);
                await PersistHistory(connection, ApplyRow(migration, history), update);
                return ApplyOutcome.Marked;
            }

            if (migration.OnError == Migration.ErrorHandling.Skip)
            {
                logger.LogWarning(ex, "Skipping {MigrationId} because onError is Skip. It was not recorded and will be retried", migration.Id);
                return ApplyOutcome.Skipped;
            }

            logger.LogError(ex, "Migration failed: {MigrationId}", migration.Id);
            return Error.From(ex);
        }
        finally
        {
            transaction?.Dispose();
            if (dispose) connection.Dispose();
        }
    }

    public async Task Baseline(Migration migration, MigrationHistory? history, CancellationToken stoppingToken = default)
    {
        var row = HistoryRow(migration, history, executedOn: history?.ExecutedOn, executedSequence: null);
        var (connection, dispose) = await Lease(stoppingToken);
        try
        {
            await PersistHistory(connection, row, update: history is not null);
        }
        finally
        {
            if (dispose) connection.Dispose();
        }
    }

    public async Task Repair(Migration migration, MigrationHistory history, CancellationToken stoppingToken = default)
    {
        var row = history with
        {
            Hash = migration.Hash,
            DeploymentId = _migrationLock?.DeploymentId
        };

        var (connection, dispose) = await Lease(stoppingToken);
        try
        {
            await connection.ExecuteAsync(dbProvider.UpdateHistory, row);
        }
        finally
        {
            if (dispose) connection.Dispose();
        }
    }

    private MigrationHistory ApplyRow(Migration migration, MigrationHistory? existing) =>
        HistoryRow(migration, existing, DateTime.UtcNow, NextSequence(existing));

    private MigrationHistory HistoryRow(
        Migration migration,
        MigrationHistory? existing,
        DateTimeOffset? executedOn,
        int? executedSequence)
    {
        if (existing is null)
        {
            return new()
            {
                FileName = migration.FileName,
                Title = migration.Title,
                Hash = migration.Hash,
                ExecutedOn = executedOn,
                ExecutedSequence = executedSequence,
                DeploymentId = _migrationLock?.DeploymentId
            };
        }

        return existing with
        {
            Hash = migration.Hash,
            ExecutedOn = executedOn,
            ExecutedSequence = executedSequence,
            DeploymentId = _migrationLock?.DeploymentId
        };
    }

    private int? NextSequence(MigrationHistory? existing) =>
        existing?.ExecutedSequence ?? ++_lastExecutedSequence;

    private Task PersistHistory(IDbConnection connection, MigrationHistory row, bool update, IDbTransaction? transaction = null) =>
        connection.ExecuteAsync(
            update ? dbProvider.UpdateHistory : dbProvider.InsertHistory,
            row,
            transaction: transaction);
}
