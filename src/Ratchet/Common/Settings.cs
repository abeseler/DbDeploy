namespace Ratchet.Common;

internal sealed class Settings
{
    public const string SectionName = "Deploy";
    public const string DefaultWorkingDirectory = "Migrations";
    public const string DefaultStartingFile = "ratchet.json";
    public string? Command { get; set; }
    public string WorkingDirectory { get; set; } = DefaultWorkingDirectory;
    public string StartingFile { get; set; } = DefaultStartingFile;
    public string? Contexts { get; set; }
    public string? DatabaseProvider { get; set; }
    public string? ConnectionString { get; set; }
    public int ConnectionAttempts { get; set; } = 10;
    public int ConnectionRetryDelaySeconds { get; set; } = 5;
    public int LockWaitMaxSeconds { get; set; } = 120;
    public string? OutputFile { get; set; }
}
