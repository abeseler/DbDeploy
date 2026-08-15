namespace Ratchet.Common;

internal static class Exceptions
{
    public static Exception FailedToAcquireLock => new("Failed to acquire deployment lock");
    public static Exception FileDoesNotExist => new("File does not exist");
    public static Exception PathDoesNotExist => new("Path does not exist");
    public static Exception StartingFileDoesNotExist(string path) => new($"Starting file does not exist: {path}");
    public static Exception StartingFileExtensionNotSupported(string extension) => new($"Starting file extension is not supported: {extension}");
    public static Exception FileIsEmpty => new("File has no migrations");
    public static Exception FileParsingError => new("Error parsing file");
    public static Exception DuplicateTitle(string title) => new($"Duplicate migration title: [{title}]");
    public static Exception MigrationsParsingError(int errorCount) => new($"Encountered {errorCount} error{(errorCount > 1 ? "s" : "")} attempting to parse migration files");
    public static Exception DependencyNotFound(string migrationId, string reference) => new($"{migrationId}\n\nDeclares a dependsOn reference that does not match any migration: {reference}");
    public static Exception DependencyAmbiguous(string migrationId, string reference) => new($"{migrationId}\n\nDeclares a dependsOn reference that matches more than one migration (case-insensitively): {reference}");
    public static Exception DependencyFilteredOut(string migrationId, string reference) => new($"{migrationId}\n\nDeclares a dependsOn reference whose migrations are all excluded by the active context: {reference}");
    public static Exception DependencyCycle(string cyclePath) => new($"Migration dependency cycle detected:\n\n{cyclePath}");
    public static Exception MigrationHasInvalidChange(string title) => new($"{title}\n\nContents have been changed since it was applied. Run repair to accept the current hash. Use runOnChange only if this object should re-apply when the SQL changes; runAlways re-runs it on every update and is not the fix for drift.\n");
    public static Exception DeploymentFailed(int notAppliedCount) => new($"Deployment failed. {notAppliedCount} migration{(notAppliedCount != 1 ? "s" : "")} not applied");
    public static Exception ValidationNeedsRepair(int count) => new($"Validation failed. {count} migration{(count != 1 ? "s" : "")} need repair (contents changed since apply)");
    public static Exception UpdateNeedsRepair(int count) => new($"Update refused. {count} migration{(count != 1 ? "s" : "")} need repair (contents changed since apply). Run repair to accept the current SQL.");
}
