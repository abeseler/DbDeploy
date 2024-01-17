using System.Security.Cryptography;

namespace DbDeploy.Data;

public sealed class Migration
{
    public MigrationType Type => this switch
    {
        { SqlStatements.Length: > 0, Title: not null, FileName: not null } => MigrationType.Sql,
        { Include: not null } => MigrationType.Include,
        { IncludeAll: not null } => MigrationType.IncludeAll,
        _ => MigrationType.Invalid
    };
    public string FileName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string[] SqlStatements { get; set; } = [];
    public string? Hash { get; set; }
    public bool RunAlways { get; set; } = false;
    public bool RunOnChange { get; set; } = false;
    public int? Timeout { get; set; }
    public ErrorHandling OnError { get; set; } = ErrorHandling.Fail;
    public bool RequireContext { get; set; } = false;
    public string[] ContextFilter { get; set; } = [];
    public string? Include { get; set; }
    public string? IncludeAll { get; set; }

    public string GetKey()
    {
        return $"{FileName} [{Title}]";
    }

    public bool IsMissingRequiredContext(string[] contexts)
    {
        if (contexts.Length == 0 && RequireContext)
            return true;

        if (ContextFilter.Length > 0 && ContextFilter.Intersect(contexts).Any() is false && (RequireContext || contexts.Length > 0))
        {
            return true;
        }

        return false;
    }

    public static string CalculateHash(string fileName, string title, string sql)
    {
        var bytes = Encoding.UTF8.GetBytes($"{fileName} {title} {sql}");
        return BitConverter.ToString(SHA256.HashData(bytes)).Replace("-", string.Empty);
    }

    public enum ErrorHandling
    {
        Fail,
        Skip,
        Mark,
    }

    public enum MigrationType
    {
        Invalid,
        Sql,
        Include,
        IncludeAll
    }
}
