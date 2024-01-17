namespace DbDeploy.Data;

internal sealed record MigrationLock
{
    public int DeploymentId { get; init; }
    public DateTime StartedOn { get; init; }
    public DateTime FinishedOn { get; set; }
}
