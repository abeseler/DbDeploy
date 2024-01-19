namespace DbDeploy.Models;

internal sealed record MigrationHistory
{
    private string? _migrationId;
    public int Id { get; init; }
    public required string FileName { get; init; }
    public required string Title { get; init; }
    public DateTime? ExecutedOn { get; set; }
    public int? ExecutedSequence { get; set; }
    public string? Hash { get; set; }
    public int? DeploymentId { get; set; }

    public string MigrationId => _migrationId ??= Migration.GenerateId(FileName, Title);
}
