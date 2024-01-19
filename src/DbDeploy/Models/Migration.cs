namespace DbDeploy.Models;

internal sealed record Migration
{
    public required string FileName { get; init; }
    public required string Title { get; init; }
    public required string[] SqlStatements { get; init; }
    public string? Hash { get; init; }
    public bool RunAlways { get; init; }
    public bool RunOnChange { get; init; }
    public bool RunInTransaction { get; init; }
    public bool RequireContext { get; init; }
    public required string[] ContextFilter { get; init; }
    public int Timeout { get; init; }
    public ErrorHandling OnError { get; init; }

    public string GetKey()
    {
        return GetKey(FileName, Title);
    }

    public static string GetKey(string fileName, string title)
    {
        return $"{fileName} [{title}]";
    }

    public enum ErrorHandling
    {
        Fail,
        Skip,
        Mark,
    }
}
