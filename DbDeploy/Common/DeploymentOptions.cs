namespace DbDeploy.Common;

internal sealed class DeploymentOptions
{
    public const string SectionName = "Deployment";
    public string? Command { get; set; } = "update";
    public string? Contexts { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? MigrationFile { get; set; }
    public required string ConnectionString { get; set; }
    public int MaxLockWaitSeconds { get; set; } = 60;
}
