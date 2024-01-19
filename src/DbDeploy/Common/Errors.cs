namespace DbDeploy.Common;

internal static class Errors
{
    public static Error FailedToAcquireLock => new("Failed to acquire deployment lock");
    public static Error DirectoryDoesNotExist => new("Directory does not exist");
    public static Error FileDoesNotExist => new("File does not exist");
    public static Error FileIsEmpty => new("File has no migrations");
    public static Error FileParsingError => new("Error parsing file");
    public static Error DuplicateTitle(string title) => new($"Duplicate migration title: [{title}]");
    public static Error MigrationsParsingError(int errorCount) => new($"Encountered {errorCount} error{(errorCount > 1 ? "s" : "")} attempting to parse migration files");
    public static Error MigrationHasInvalidChange(string title) => new($"{title}\n\nContents have been changed since it was applied.\nSetting runOnChange or runAlways will bypass this check and force the migration to be reapplied\n");
    public static Error DeploymentFailed(int notAppliedCount) => new($"Deployment failed. {notAppliedCount} migration{(notAppliedCount != 1 ? "s" : "")} not applied");
}
