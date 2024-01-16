namespace DbDeploy.Data;

internal static class SqlStatements
{
    public const string EnsureMigrationTablesExist = """
        IF OBJECT_ID(N'[dbo].[__MigrationLock]', N'U') IS NULL
        CREATE TABLE [dbo].[__MigrationLock] (
            DeploymentId INT NOT NULL IDENTITY(1,1),
            StartedOn DATETIME2 NOT NULL,
            FinishedOn DATETIME2 NULL,
            CONSTRAINT PK_MigrationLock PRIMARY KEY CLUSTERED (DeploymentId),
            INDEX IX_MigrationLock_FinishedOn NONCLUSTERED (FinishedOn)
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
           	CONSTRAINT PK_MigrationHistory PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT UQ_MigrationHistory_Key UNIQUE NONCLUSTERED (FileName, Title)
        );
        """;

    public const string AcquireLock = """
        INSERT INTO [dbo].[MigrationLock] ([StartedOn])
        OUTPUT inserted.DeploymentId, inserted.StartedOn, inserted.FinishedOn
        SELECT GETUTCDATE()
        WHERE NOT EXISTS (SELECT * FROM [dbo].[MigrationLock] WHERE FinishedOn IS NULL);
        """;

    public const string ReleaseLock = """
        UPDATE [dbo].[MigrationLock]
        SET [FinishedOn] = GETUTCDATE(), [MigrationsApplied] = @MigrationsApplied
        WHERE [DeploymentId] = @DeploymentId;
        """;

    public const string GetExecutedMigrations = """
        SELECT [FileName], [Title], [ExecutedOn], [ExecutedSequence], [Hash], [DeploymentId]
        FROM [dbo].[MigrationHistory]
        ORDER BY [ExecutedSequence] ASC;
        """;

    public const string InsertMigrationHistory = """
        INSERT INTO [dbo].[MigrationHistory] ([FileName], [Title], [ExecutedOn], [ExecutedSequence], [Hash], [DeploymentId])
        VALUES (@FileName, @Title, @ExecutedOn, @ExecutedSequence, @Hash, @DeploymentId);
        """;

    public const string UpdateMigrationHistory = """
        UPDATE [dbo].[MigrationHistory]
        SET [ExecutedOn] = @ExecutedOn, [ExecutedSequence] = @ExecutedSequence, [Hash] = @Hash, [DeploymentId] = @DeploymentId
        WHERE [FileName] = @FileName AND [Title] = @Title;
        """;
}
