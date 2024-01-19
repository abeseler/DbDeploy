namespace DbDeploy.Data;

internal static class SqlStatements
{
    public const string EnsureMigrationTablesExist = """
        IF OBJECT_ID(N'[dbo].[__MigrationLock]', N'U') IS NULL
        CREATE TABLE [dbo].[__MigrationLock] (
            DeploymentId INT NOT NULL IDENTITY(1,1),
            StartedOn DATETIME2 NOT NULL,
            FinishedOn DATETIME2 NULL,
            CONSTRAINT PK__MigrationLock PRIMARY KEY CLUSTERED (DeploymentId),
            INDEX IX__MigrationLock_FinishedOn NONCLUSTERED (FinishedOn)
        );

        IF OBJECT_ID(N'[dbo].[__MigrationHistory]', N'U') IS NULL
        CREATE TABLE [dbo].[__MigrationHistory] (
            Id INT NOT NULL IDENTITY(1,1),
           	FileName VARCHAR(128) NOT NULL,
           	Title VARCHAR(50) NOT NULL,
           	ExecutedOn DATETIME2 NULL,
           	ExecutedSequence INT NULL,
           	Hash VARCHAR(MAX) NULL,
           	DeploymentId INT NULL,
           	CONSTRAINT PK__MigrationHistory PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT UQ__MigrationHistory_Key UNIQUE NONCLUSTERED (FileName, Title)
        );
        """;

    public const string AcquireLock = """
        INSERT INTO [dbo].[__MigrationLock] ([StartedOn])
        OUTPUT inserted.DeploymentId, inserted.StartedOn, inserted.FinishedOn
        SELECT GETUTCDATE()
        WHERE NOT EXISTS (SELECT * FROM [dbo].[__MigrationLock] WHERE FinishedOn IS NULL);
        """;

    public const string ReleaseLock = """
        UPDATE [dbo].[__MigrationLock]
        SET [FinishedOn] = GETUTCDATE()
        WHERE [DeploymentId] = @DeploymentId;
        """;

    public const string GetAllMigrationHistories = """
        SELECT [Id], [FileName], [Title], [ExecutedOn], [ExecutedSequence], [Hash], [DeploymentId]
        FROM [dbo].[__MigrationHistory];
        """;

    public const string InsertMigrationHistory = """
        INSERT INTO [dbo].[__MigrationHistory] ([FileName], [Title], [ExecutedOn], [ExecutedSequence], [Hash], [DeploymentId])
        VALUES (@FileName, @Title, @ExecutedOn, @ExecutedSequence, @Hash, @DeploymentId);
        """;

    public const string UpdateMigrationHistory = """
        UPDATE [dbo].[__MigrationHistory]
        SET [ExecutedOn] = @ExecutedOn, [ExecutedSequence] = @ExecutedSequence, [Hash] = @Hash, [DeploymentId] = @DeploymentId
        WHERE [Id] = @Id;
        """;
}
