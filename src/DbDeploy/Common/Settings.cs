namespace DbDeploy.Common;

internal sealed class Settings
{
    public const string SectionName = "Deploy";
    public const string WorkingDirectory = "Migrations";
    public string? Command { get; set; }
    public string? StartingFile { get; set; }
    public string? Contexts { get; set; }
    public string? DatabaseProvider { get; set; }
    public string? ConnectionString { get; set; }
    public int ConnectionAttempts { get; set; } = 10;
    public int ConnectionRetryDelaySeconds { get; set; } = 5;
    public int LockWaitMaxSeconds { get; set; } = 120;
    public int ShutdownWaitSeconds { get; set; } = 0;
}
