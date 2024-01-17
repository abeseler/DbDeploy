namespace DbDeploy.FileHandling;

internal sealed class MigrationIncludes
{
    public string[] Include { get; set; } = [];
    public bool ErrorIfMissingOrEmpty { get; set; } = true;
}
