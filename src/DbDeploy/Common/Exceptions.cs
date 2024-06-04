namespace DbDeploy.Common;

internal static class Exceptions
{
    public static Exception FailedToAcquireLock => new("Failed to acquire deployment lock");
    public static Exception DirectoryDoesNotExist => new("Directory does not exist");
    public static Exception FileDoesNotExist => new("File does not exist");
    public static Exception FileIsEmpty => new("File has no migrations");
    public static Exception FileParsingError => new("Error parsing file");
    public static Exception DuplicateTitle(string title) => new($"Duplicate migration title: [{title}]");
    public static Exception MigrationsParsingError(int errorCount) => new($"Encountered {errorCount} error{(errorCount > 1 ? "s" : "")} attempting to parse migration files");
    public static Exception MigrationHasInvalidChange(string title) => new($"{title}\n\nContents have been changed since it was applied.\nSetting runOnChange or runAlways will bypass this check and force the migration to be reapplied\n");
    public static Exception DeploymentFailed(int notAppliedCount) => new($"Deployment failed. {notAppliedCount} migration{(notAppliedCount != 1 ? "s" : "")} not applied");
}
