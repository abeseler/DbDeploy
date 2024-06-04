namespace DbDeploy.Models;

internal sealed record MigrationLock
{
    public int DeploymentId { get; init; }
    public DateTimeOffset StartedOn { get; init; }
    public DateTimeOffset FinishedOn { get; set; }
}
