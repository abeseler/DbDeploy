namespace Ratchet.Cli;

internal sealed class Settings
{
    public const string SectionName = "Ratchet";
    public const string DefaultWorkingDirectory = "Migrations";
    public const string DefaultStartingFile = "ratchet.json";
    public const string DefaultDatabaseProvider = "postgres";
    public string? Command { get; set; }
    public string WorkingDirectory { get; set; } = DefaultWorkingDirectory;
    public string StartingFile { get; set; } = DefaultStartingFile;
    public string? Contexts { get; set; }
    public string[] ParseContexts() =>
        Contexts?.Split(',').Select(x => x.Trim()).ToArray() ?? [];
    public string DatabaseProvider { get; set; } = DefaultDatabaseProvider;
    public string ResolveDatabaseProvider() =>
        string.IsNullOrWhiteSpace(DatabaseProvider)
            ? DefaultDatabaseProvider
            : DatabaseProvider.Trim().ToLowerInvariant();
    public string? ConnectionString { get; set; }
    public bool IsDatabaseConfigured =>
        string.IsNullOrWhiteSpace(ConnectionString) is false;
    public int ConnectionAttempts { get; set; } = 10;
    public int ConnectionRetryDelaySeconds { get; set; } = 5;
    public int LockWaitMaxSeconds { get; set; } = 120;
    public string? OutputFile { get; set; }
}
