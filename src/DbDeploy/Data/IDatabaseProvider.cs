using System.Data;
using System.Globalization;

namespace DbDeploy.Data;

public interface IDatabaseProvider
{
    public Task<IDbConnection> ConnectAsync(CancellationToken cancellationToken);
    public string EnsureMigrationTablesExist { get; }
    public string AcquireLock { get; }
    public string ReleaseLock { get; }
    public string GetAllMigrationHistories { get; }
    public string InsertMigrationHistory { get; }
    public string UpdateMigrationHistory { get; }
}

internal sealed class PostgresDbProvider(string connectionString) : IDatabaseProvider
{
    private readonly string _connectionString = new Npgsql.NpgsqlConnectionStringBuilder(connectionString).ConnectionString;
    public async Task<IDbConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var connection = new Npgsql.NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
    public string EnsureMigrationTablesExist => """
        CREATE TABLE IF NOT EXISTS __migration_lock (
            deployment_id INT GENERATED ALWAYS AS IDENTITY,
            started_on TIMESTAMP NOT NULL,
            finished_on TIMESTAMP NULL,
            CONSTRAINT pk__migration_lock PRIMARY KEY (deployment_id)
        );
        
        CREATE INDEX IF NOT EXISTS ix__migration_lock__finished_on ON public.__migration_lock (finished_on);
                    
        CREATE TABLE IF NOT EXISTS __migration_history (
            id INT GENERATED ALWAYS AS IDENTITY,
            file_name VARCHAR(500) NOT NULL,
            title VARCHAR(250) NOT NULL,
            executed_on TIMESTAMP NULL,
            executed_sequence INT NULL,
            hash TEXT NULL,
            deployment_id INT NULL,
            CONSTRAINT pk__migration_history PRIMARY KEY (id),
            CONSTRAINT uq__migration_history__key UNIQUE (file_name, title)
        );
        """;
    public string AcquireLock => """
        INSERT INTO __migration_lock (started_on)
        SELECT NOW() AT TIME ZONE 'utc'
        WHERE NOT EXISTS (SELECT 1 FROM __migration_lock WHERE finished_on IS NULL)
        RETURNING deployment_id, started_on, finished_on;
        """;
    public string ReleaseLock => """
        UPDATE __migration_lock
        SET finished_on = NOW() AT TIME ZONE 'utc'
        WHERE deployment_id = @DeploymentId;
        """;
    public string GetAllMigrationHistories => """
        SELECT id, file_name, title, executed_on, executed_sequence, hash, deployment_id
        FROM __migration_history;
        """;
    public string InsertMigrationHistory => """
        INSERT INTO __migration_history (file_name, title, executed_on, executed_sequence, hash, deployment_id)
        VALUES (@FileName, @Title, @ExecutedOn, @ExecutedSequence, @Hash, @DeploymentId);
        """;
    public string UpdateMigrationHistory => """
        UPDATE __migration_history
        SET executed_on = @ExecutedOn, executed_sequence = @ExecutedSequence, hash = @Hash, deployment_id = @DeploymentId
        WHERE id = @Id;
        """;
}
internal sealed class MsSqlDbProvider(string connectionString) : IDatabaseProvider
{
    private readonly string _connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString).ConnectionString;
    public async Task<IDbConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
    public string EnsureMigrationTablesExist => """
        IF OBJECT_ID(N'[__migration_lock]', N'U') IS NULL
        CREATE TABLE [__migration_lock] (
            deployment_id INT NOT NULL IDENTITY(1,1),
            started_on DATETIMEOFFSET NOT NULL,
            finished_on DATETIMEOFFSET NULL,
            CONSTRAINT pk__migration_lock PRIMARY KEY CLUSTERED (deployment_id),
            INDEX ix__migration_lock__finished_on NONCLUSTERED (finished_on)
        );

        IF OBJECT_ID(N'[__migration_history]', N'U') IS NULL
        CREATE TABLE [__migration_history] (
            id INT NOT NULL IDENTITY(1,1),
            file_name VARCHAR(500) NOT NULL,
            title VARCHAR(250) NOT NULL,
            executed_on DATETIMEOFFSET NULL,
            executed_sequence INT NULL,
            hash VARCHAR(MAX) NULL,
            deployment_id INT NULL,
            CONSTRAINT pk__migration_history PRIMARY KEY CLUSTERED (id),
            CONSTRAINT uq__migration_history__key UNIQUE NONCLUSTERED (file_name, title)
        );
        """;
    public string AcquireLock => """
        INSERT INTO [__migration_lock] ([started_on])
        OUTPUT inserted.deployment_id, inserted.started_on, inserted.finished_on
        SELECT GETUTCDATE()
        WHERE NOT EXISTS (SELECT * FROM [__migration_lock] WHERE finished_on IS NULL);
        """;
    public string ReleaseLock => """
        UPDATE [__migration_lock]
        SET [finished_on] = GETUTCDATE()
        WHERE [deployment_id] = @DeploymentId;
        """;
    public string GetAllMigrationHistories => """
        SELECT [id], [file_name], [title], [executed_on], [executed_sequence], [hash], [deployment_id]
        FROM [__migration_history];
        """;
    public string InsertMigrationHistory => """
        INSERT INTO [__migration_history] ([file_name], [title], [executed_on], [executed_sequence], [hash], [deployment_id])
        VALUES (@FileName, @Title, @ExecutedOn, @ExecutedSequence, @Hash, @DeploymentId);
        """;
    public string UpdateMigrationHistory => """
        UPDATE [__migration_history]
        SET [executed_on] = @ExecutedOn, [executed_sequence] = @ExecutedSequence, [hash] = @Hash, [deployment_id] = @DeploymentId
        WHERE [id] = @Id;
        """;
}
internal sealed class SqliteDbProvider : IDatabaseProvider
{
    private readonly string _connectionString;

    static SqliteDbProvider()
    {
        Dapper.SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
    }

    public SqliteDbProvider(string connectionString)
    {
        _connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).ConnectionString;
    }

    public async Task<IDbConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
    public string EnsureMigrationTablesExist => """
        CREATE TABLE IF NOT EXISTS __migration_lock (
            deployment_id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            started_on TEXT NOT NULL,
            finished_on TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS ix__migration_lock__finished_on ON __migration_lock (finished_on);

        CREATE TABLE IF NOT EXISTS __migration_history (
            id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            file_name TEXT NOT NULL,
            title TEXT NOT NULL,
            executed_on TEXT NULL,
            executed_sequence INTEGER NULL,
            hash TEXT NULL,
            deployment_id INTEGER NULL,
            CONSTRAINT uq__migration_history__key UNIQUE (file_name, title)
        );
        """;
    public string AcquireLock => """
        INSERT INTO __migration_lock (started_on)
        SELECT strftime('%Y-%m-%d %H:%M:%f', 'now') || '+00:00'
        WHERE NOT EXISTS (SELECT 1 FROM __migration_lock WHERE finished_on IS NULL)
        RETURNING deployment_id, started_on, finished_on;
        """;
    public string ReleaseLock => """
        UPDATE __migration_lock
        SET finished_on = strftime('%Y-%m-%d %H:%M:%f', 'now') || '+00:00'
        WHERE deployment_id = @DeploymentId;
        """;
    public string GetAllMigrationHistories => """
        SELECT id, file_name, title, executed_on, executed_sequence, hash, deployment_id
        FROM __migration_history;
        """;
    public string InsertMigrationHistory => """
        INSERT INTO __migration_history (file_name, title, executed_on, executed_sequence, hash, deployment_id)
        VALUES (@FileName, @Title, @ExecutedOn, @ExecutedSequence, @Hash, @DeploymentId);
        """;
    public string UpdateMigrationHistory => """
        UPDATE __migration_history
        SET executed_on = @ExecutedOn, executed_sequence = @ExecutedSequence, hash = @Hash, deployment_id = @DeploymentId
        WHERE id = @Id;
        """;

    private sealed class DateTimeOffsetHandler : Dapper.SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value) => value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt, TimeSpan.Zero),
            string s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
            _ => throw new InvalidCastException($"Cannot convert {value?.GetType().Name ?? "null"} to DateTimeOffset")
        };

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.Value = value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
        }
    }
}
