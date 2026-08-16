namespace Ratchet.Models;

internal sealed record MigrationHistory
{
    private string? _migrationId;
    public int Id { get; init; }
    public required string FileName { get; init; }
    public required string Title { get; init; }
    public DateTimeOffset? ExecutedOn { get; init; }
    public int? ExecutedSequence { get; init; }
    public string? Hash { get; init; }
    public int? DeploymentId { get; init; }

    public string MigrationId => _migrationId ??= Migration.GenerateId(FileName, Title);
}
