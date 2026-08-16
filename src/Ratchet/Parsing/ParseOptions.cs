namespace Ratchet.Parsing;

internal sealed class ParseOptions
{
    public string[] ContextFilter { get; init; } = [];
    public bool ContextRequired { get; init; }
    public bool ErrorIfMissingOrEmpty { get; init; } = true;

    public static ParseOptions FromInclude(MigrationIncludes include) => new()
    {
        ContextFilter = include.ContextFilter,
        ContextRequired = include.ContextRequired,
        ErrorIfMissingOrEmpty = include.ErrorIfMissingOrEmpty
    };
}
