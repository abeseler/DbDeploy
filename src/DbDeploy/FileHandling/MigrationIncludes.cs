namespace DbDeploy.FileHandling;

internal sealed class MigrationIncludes
{
    public required string[] Include { get; init; }
    public string[] ContextFilter { get; init; } = [];
    public bool RequiresContext { get; init; }
    public bool ErrorIfMissingOrEmpty { get; init; } = true;
}
