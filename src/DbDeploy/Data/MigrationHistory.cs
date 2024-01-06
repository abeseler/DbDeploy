namespace DbDeploy.Data;

internal sealed class MigrationHistory
{
    public required string FileName { get; set; }
    public required string Title { get; set; }
    public DateTime? ExecutedOn { get; set; }
    public int? ExecutedSequence { get; set; }
    public string? Hash { get; set; }
    public int? DeploymentId { get; set; }

    public string GetKey()
    {
        return $"{FileName} [{Title}]";
    }
}
