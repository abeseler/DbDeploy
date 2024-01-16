namespace DbDeploy.Data;

internal sealed class MigrationLock
{
    public int DeploymentId { get; init; }
    public DateTime StartedOn { get; init; }
    public DateTime FinishedOn { get; set; }
}
