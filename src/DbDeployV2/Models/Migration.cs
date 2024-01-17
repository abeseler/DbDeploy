using System.Security.Cryptography;
using System.Text;

namespace DbDeploy.Data;

public sealed record Migration
{
    public required string FileName { get; init; }
    public required string Title { get; init; }
    public required string[] SqlStatements { get; init; }
    public string? Hash { get; init; }
    public bool RunAlways { get; init; }
    public bool RunOnChange { get; init; }
    public int Timeout { get; init; } = 30;
    public ErrorHandling OnError { get; init; } = ErrorHandling.Fail;

    public string GetKey()
    {
        return GetKey(FileName, Title);
    }

    public static string GetKey(string fileName, string title)
    {
        return $"{fileName} [{title}]";
    }

    public static string CalculateHash(string sql)
    {
        var bytes = Encoding.UTF8.GetBytes(sql);
        return BitConverter.ToString(MD5.HashData(bytes)).Replace("-", string.Empty);
    }

    public enum ErrorHandling
    {
        Fail,
        Skip,
        Mark,
    }
}
