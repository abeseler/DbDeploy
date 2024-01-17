namespace DbDeploy.Common;

internal static class Errors
{
    public static Error FileDoesNotExist(string file) => new($"File does not exist: {file}");
    public static Error FileIsEmpty(string file) => new($"File has no migrations: {file}");
    public static Error CommandNotImplemented => new($"Command has not been implemented");
}
