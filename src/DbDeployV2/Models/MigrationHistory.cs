namespace DbDeploy.Data;

[DebuggerDisplay("{GetKey()}")]
internal sealed class MigrationHistory
{
    public required string FileName { get; init; }
    public required string Title { get; init; }
    public DateTime? ExecutedOn { get; set; }
    public int? ExecutedSequence { get; set; }
    public string? Hash { get; set; }
    public int? DeploymentId { get; set; }

    public string GetKey()
    {
        return $"{FileName} [{Title}]";
    }
}
