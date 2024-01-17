namespace DbDeploy.Common;

internal sealed class Settings
{
    public const string SectionName = "Deploy";
    public string? Command { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? StartingFile { get; set; }
    public string? ConnectionString { get; set; }
    public int MaxLockWaitSeconds { get; set; } = 120;
}
