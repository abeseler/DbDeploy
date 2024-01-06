using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DbDeploy.FileHandling;

internal static partial class SqlFileParser
{
    public static Result<List<Migration>, Exception> Parse(string text, string fileName)
    {
        List<Migration> migrations = [];
        var migrationData = MigrationSectionRegex().Matches(text).AsEnumerable();

        foreach (var migration in migrationData)
        {
            var content = migration.Groups["content"].Value;
            var properties = KeyValueRegex().Matches(content)
                                            .Select(m => new KeyValuePair<string, string>(m.Groups["key"].Value, m.Groups["value"].Value))
                                            .ToDictionary();
            var title = properties.GetStringOrDefault("title", null)?.Trim();

            if (string.IsNullOrEmpty(title))
                return new ValidationException($"Missing migration title in {fileName}");

            if (migrations.Any(m => m.Title == title))
                return new ValidationException($"Duplicate migration title in {fileName}: {title}");

            var sql = migration.Groups["sql"].Value;
            var sqlStatements = ParseSqlStatements(sql, properties.GetBoolOrDefault("splitStatements", true));

            if (sqlStatements.Length == 0)
                return new ValidationException($"Empty migration found in {fileName}");

            migrations.Add(new()
            {
                FileName = fileName,
                Title = title,
                SqlStatements = sqlStatements,
                Hash = Migration.CalculateHash(fileName, title, sql),
                RunAlways = properties.GetBoolOrDefault("runAlways", false),
                RunOnChange = properties.GetBoolOrDefault("runOnChange", false),
                Timeout = properties.GetIntOrDefault("timeout", null),
                OnError = properties.GetEnumOrDefault("onError", Migration.ErrorHandling.Fail),
                RequireContext = properties.GetBoolOrDefault("requireContext", false),
                ContextFilter = properties.GetStringArray("contextFilter", ',')
            });
        }

        return migrations;
    }

    public static string[] ParseSqlStatements(string sql, bool splitStatements)
    {
        return splitStatements ? SqlStatementSplitterRegex()
                                    .Split(sql)
                                    .Where(x => !string.IsNullOrWhiteSpace(x))
                                    .Select(x => x.Trim())
                                    .ToArray()
            : [sql.Trim()];
    }

    private static string? GetStringOrDefault(this IReadOnlyDictionary<string, string> dictionary, string key, string? defaultValue) =>
        dictionary.TryGetValue(key, out var value) ? value : defaultValue;

    private static string[] GetStringArray(this IReadOnlyDictionary<string, string> dictionary, string key, char separator) =>
        dictionary.TryGetValue(key, out var value) ? value.Split(separator).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray() : [];

    private static bool GetBoolOrDefault(this IReadOnlyDictionary<string, string> dictionary, string key, bool defaultValue) =>
        dictionary.TryGetValue(key, out var value) && bool.TryParse(value, out var boolValue) ? boolValue : defaultValue;

    private static int? GetIntOrDefault(this IReadOnlyDictionary<string, string> dictionary, string key, int? defaultValue) =>
        dictionary.TryGetValue(key, out var value) && int.TryParse(value, out var intValue) ? intValue : defaultValue;

    private static T GetEnumOrDefault<T>(this IReadOnlyDictionary<string, string> dictionary, string key, T defaultValue) where T : struct, Enum =>
        dictionary.TryGetValue(key, out var value) && Enum.TryParse<T>(value, true, out var enumValue) ? enumValue : defaultValue;

    [GeneratedRegex("""/\*\s*Migration(?<content>[\s\S]*?)\*/\s*(?<sql>[\s\S]*?)(?=(/\* Migration|$))""", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex MigrationSectionRegex();
    [GeneratedRegex("""(?<key>\w+)\s*=\s*(?:""(?<value>[^""]*)""|(?<value>[^\s].*?))(?=\s+\w+\s*=|$)""", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex KeyValueRegex();
    [GeneratedRegex(@"^GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline, "en-US")]
    private static partial Regex SqlStatementSplitterRegex();
}
