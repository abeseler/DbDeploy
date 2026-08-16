namespace Ratchet.Models;

internal sealed record Migration
{
    private string? _id;
    public required string FileName { get; init; }
    public required string Title { get; init; }
    public required string[] SqlStatements { get; init; }
    public string[] DependsOn { get; init; } = [];
    public string? Hash { get; init; }
    public RunMode Run { get; init; } = RunMode.Once;
    public bool RunInTransaction { get; init; }
    public bool ContextRequired { get; init; }
    public required string[] ContextFilter { get; init; }
    public int Timeout { get; init; }
    public ErrorHandling OnError { get; init; }
    public string Id => _id ??= GenerateId(FileName, Title);
    public static string GenerateId(string fileName, string title) => $"{fileName} [{title}]";

    public bool IsMissingRequiredContext(string[] contexts) => (this, contexts) switch
    {
        ({ ContextRequired: true }, { Length: 0 }) => true,
        ({ ContextFilter.Length: > 0 }, { Length: > 0 }) => ContextFilter.Intersect(contexts).Any() is not true,
        _ => false
    };

    public bool HasDrift(MigrationHistory? history) =>
        Run is RunMode.Once && history is { Hash: not null } && Hash != history.Hash;

    public enum RunMode
    {
        Once,
        OnChange,
        Always,
        Never
    }

    public enum ErrorHandling
    {
        Fail,
        Skip,
        Mark,
    }
}
