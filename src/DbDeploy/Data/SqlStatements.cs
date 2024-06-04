namespace DbDeploy.Data;

internal static class SqlStatements
{
    public static string EnsureMigrationTablesExistQuery(string dbProvider) =>
        dbProvider switch
        {
            "mssql" => MsSql.EnsureMigrationTablesExist,
            "postgres" => Postgres.EnsureMigrationTablesExist,
            _ => throw new NotSupportedException($"Database provider '{dbProvider}' is not supported.")
        };

    public static string AcquireLockQuery(string dbProvider) =>
        dbProvider switch
        {
            "mssql" => MsSql.AcquireLock,
            "postgres" => Postgres.AcquireLock,
            _ => throw new NotSupportedException($"Database provider '{dbProvider}' is not supported.")
        };

    public static string ReleaseLockQuery(string dbProvider) =>
        dbProvider switch
        {
            "mssql" => MsSql.ReleaseLock,
            "postgres" => Postgres.ReleaseLock,
            _ => throw new NotSupportedException($"Database provider '{dbProvider}' is not supported.")
        };

    public static string GetAllMigrationHistoriesQuery(string dbProvider) =>
        dbProvider switch
        {
            "mssql" => MsSql.GetAllMigrationHistories,
            "postgres" => Postgres.GetAllMigrationHistories,
            _ => throw new NotSupportedException($"Database provider '{dbProvider}' is not supported.")
        };

    public static string InsertMigrationHistoryQuery(string dbProvider) =>
        dbProvider switch
        {
            "mssql" => MsSql.InsertMigrationHistory,
            "postgres" => Postgres.InsertMigrationHistory,
            _ => throw new NotSupportedException($"Database provider '{dbProvider}' is not supported.")
        };

    public static string UpdateMigrationHistoryQuery(string dbProvider) =>
        dbProvider switch
        {
            "mssql" => MsSql.UpdateMigrationHistory,
            "postgres" => Postgres.UpdateMigrationHistory,
            _ => throw new NotSupportedException($"Database provider '{dbProvider}' is not supported.")
        };

    private static class Postgres
    {
        public const string EnsureMigrationTablesExist = """
            CREATE TABLE IF NOT EXISTS public.__migration_lock (
                deployment_id INT GENERATED ALWAYS AS IDENTITY,
                started_on TIMESTAMP NOT NULL,
                finished_on TIMESTAMP NULL,
                CONSTRAINT pk__migration_lock PRIMARY KEY (deployment_id)
            );

            CREATE INDEX IF NOT EXISTS ix__migration_lock__finished_on ON public.__migration_lock (finished_on);
                        
            CREATE TABLE IF NOT EXISTS public.__migration_history (
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

        public const string AcquireLock = """
            INSERT INTO public.__migration_lock (started_on)
            SELECT NOW() AT TIME ZONE 'utc'
            WHERE NOT EXISTS (SELECT 1 FROM public.__migration_lock WHERE finished_on IS NULL)
            RETURNING deployment_id, started_on, finished_on;
            """;

        public const string ReleaseLock = """
            UPDATE public.__migration_lock
            SET finished_on = NOW() AT TIME ZONE 'utc'
            WHERE deployment_id = @DeploymentId;
            """;

        public const string GetAllMigrationHistories = """
            SELECT id, file_name, title, executed_on, executed_sequence, hash, deployment_id
            FROM public.__migration_history;
            """;

        public const string InsertMigrationHistory = """
            INSERT INTO public.__migration_history (file_name, title, executed_on, executed_sequence, hash, deployment_id)
            VALUES (@FileName, @Title, @ExecutedOn, @ExecutedSequence, @Hash, @DeploymentId);
            """;

        public const string UpdateMigrationHistory = """
            UPDATE public.__migration_history
            SET executed_on = @ExecutedOn, executed_sequence = @ExecutedSequence, hash = @Hash, deployment_id = @DeploymentId
            WHERE id = @Id;
            """;
    }

    private static class MsSql
    {
        public const string EnsureMigrationTablesExist = """
            IF OBJECT_ID(N'[dbo].[__migration_lock]', N'U') IS NULL
            CREATE TABLE [dbo].[__migration_lock] (
                deployment_id INT NOT NULL IDENTITY(1,1),
                started_on DATETIMEOFFSET NOT NULL,
                finished_on DATETIMEOFFSET NULL,
                CONSTRAINT pk__migration_lock PRIMARY KEY CLUSTERED (deployment_id),
                INDEX ix__migration_lock__finished_on NONCLUSTERED (finished_on)
            );

            IF OBJECT_ID(N'[dbo].[__migration_history]', N'U') IS NULL
            CREATE TABLE [dbo].[__migration_history] (
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

        public const string AcquireLock = """
            INSERT INTO [dbo].[__migration_lock] ([started_on])
            OUTPUT inserted.deployment_id, inserted.started_on, inserted.finished_on
            SELECT GETUTCDATE()
            WHERE NOT EXISTS (SELECT * FROM [dbo].[__migration_lock] WHERE finished_on IS NULL);
            """;

        public const string ReleaseLock = """
            UPDATE [dbo].[__migration_lock]
            SET [finished_on] = GETUTCDATE()
            WHERE [deployment_id] = @DeploymentId;
            """;

        public const string GetAllMigrationHistories = """
            SELECT [id], [file_name], [title], [executed_on], [executed_sequence], [hash], [deployment_id]
            FROM [dbo].[__migration_history];
            """;

        public const string InsertMigrationHistory = """
            INSERT INTO [dbo].[__migration_history] ([file_name], [title], [executed_on], [executed_sequence], [hash], [deployment_id])
            VALUES (@FileName, @Title, @ExecutedOn, @ExecutedSequence, @Hash, @DeploymentId);
            """;

        public const string UpdateMigrationHistory = """
            UPDATE [dbo].[__migration_history]
            SET [executed_on] = @ExecutedOn, [executed_sequence] = @ExecutedSequence, [hash] = @Hash, [deployment_id] = @DeploymentId
            WHERE [id] = @Id;
            """;
    }
}
