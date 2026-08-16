namespace Ratchet;

internal static class Errors
{
    public static Error FailedToAcquireLock => new("Failed to acquire deployment lock");
    public static Error PathDoesNotExist => new("Path does not exist");
    public static Error StartingFileDoesNotExist(string path) => new($"Starting file does not exist: {path}");
    public static Error StartingFileExtensionNotSupported(string extension) => new($"Starting file extension is not supported: {extension}");
    public static Error StartingFileParseFailed(string message) => new($"Error parsing starting file: {message}");
    public static Error FileIsEmpty => new("File has no migrations");
    public static Error FileParsingError => new("Error parsing file");
    public static Error UnclosedMigrationHeader => new("Unclosed migration header");
    public static Error DuplicateTitle(string title) => new($"Duplicate migration title: [{title}]");
    public static Error ReplacedRunFlags => new("runAlways and runOnChange were replaced by run: once | onChange | always | never");
    public static Error InvalidRunValue(string value) => new($"Invalid run value '{value}'. Use once, onChange, always, or never.");
    public static Error MigrationsParsingError(int errorCount) => new($"Encountered {errorCount} error{(errorCount > 1 ? "s" : "")} attempting to parse migration files");
    public static Error DependencyNotFound(string migrationId, string reference) => new($"{migrationId}\n\nDeclares a dependsOn reference that does not match any migration: {reference}");
    public static Error DependencyAmbiguous(string migrationId, string reference) => new($"{migrationId}\n\nDeclares a dependsOn reference that matches more than one migration (case-insensitively): {reference}");
    public static Error DependencyFilteredOut(string migrationId, string reference) => new($"{migrationId}\n\nDeclares a dependsOn reference whose migrations are all excluded by the active context: {reference}");
    public static Error DependencyCycle(string cyclePath) => new($"Migration dependency cycle detected:\n\n{cyclePath}");
    public static Error MigrationHasDrift(string title) => new($"{title}\n\nContents have been changed since it was applied. Run repair to accept the current hash. Set run to onChange only if this object should re-apply when the SQL changes; always re-runs it on every update and is not the fix for drift.\n");
    public static Error DeploymentFailed(int notAppliedCount) => new($"Deployment failed. {notAppliedCount} migration{(notAppliedCount != 1 ? "s" : "")} not applied");
    public static Error ValidationNeedsRepair(int count) => new($"Validation failed. {count} migration{(count != 1 ? "s" : "")} need repair (contents changed since apply)");
    public static Error UpdateNeedsRepair(int count) => new($"Update refused. {count} migration{(count != 1 ? "s" : "")} need repair (contents changed since apply). Run repair to accept the current SQL.");
}
