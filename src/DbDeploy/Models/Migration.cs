namespace DbDeploy.Models;

internal sealed record Migration
{
    private string? _id;
    public required string FileName { get; init; }
    public required string Title { get; init; }
    public required string[] SqlStatements { get; init; }
    public string? Hash { get; init; }
    public bool RunAlways { get; init; }
    public bool RunOnChange { get; init; }
    public bool RunInTransaction { get; init; }
    public bool RequiresContext { get; init; }
    public required string[] ContextFilter { get; init; }
    public int Timeout { get; init; }
    public ErrorHandling OnError { get; init; }
    public string Id => _id ??= GenerateId(FileName, Title);
    public static string GenerateId(string fileName, string title) => $"{fileName} [{title}]";

    public bool IsMissingRequiredContext(string[] contexts) => (this, contexts) switch
    {
        ({ RequiresContext: true }, { Length: 0 }) => true,
        ({ ContextFilter.Length: > 0 }, { Length: > 0 }) => ContextFilter.Intersect(contexts).Any() is not true,
        _ => false
    };

    public bool HasInvalidChange(MigrationHistory? history) => (this, history) switch
    {
        ({ RunAlways: false, RunOnChange: false }, { Hash: not null }) => Hash != history.Hash,
        _ => false
    };

    public enum ErrorHandling
    {
        Fail,
        Skip,
        Mark,
    }
}
