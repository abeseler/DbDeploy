using DbDeployV1.Commands;
using DbDeployV1.Data;
using System.ComponentModel.DataAnnotations;

namespace DbDeployV1.FileHandling;

internal static class JsonFileParser
{
    public static Result<List<Migration>, Exception> Parse(string text, string fileName)
    {
        var migrationJsons = JsonSerializer.Deserialize<MigrationJson[]>(text) ?? [];
        var migrations = new List<Migration>();

        foreach (var migrationJson in migrationJsons)
        {
            var migration = new Migration
            {
                FileName = fileName,
                Title = migrationJson.Title,
                Include = migrationJson.Include,
                IncludeAll = migrationJson.IncludeAll,
                SqlStatements = migrationJson.Sql is not null ? SqlFileParser.ParseSqlStatements(migrationJson.Sql, migrationJson.SplitStatements) : [],
                RunAlways = migrationJson.RunAlways,
                RunOnChange = migrationJson.RunOnChange,
                Timeout = migrationJson.Timeout,
                OnError = Enum.TryParse<Migration.ErrorHandling>(migrationJson.OnError, out var onError) ? onError : Migration.ErrorHandling.Fail,
                RequireContext = migrationJson.RequireContext,
                ContextFilter = migrationJson.ContextFilter?.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray() ?? []
            };

            if (migration.Type == Migration.MigrationType.Sql && migrations.Any(x => x.Title == migration.Title))
                return new ValidationException($"File '{fileName}' has a duplicate migration title: {migration.Title}");

            if (migration.Type == Migration.MigrationType.Sql)
                migration.Hash = Migration.CalculateHash(fileName, migrationJson.Title!, migrationJson.Sql!);

            migrations.Add(migration);
        }

        return migrations;
    }

    private sealed class MigrationJson
    {
        public string? Title { get; set; }
        public string? Sql { get; set; }
        public string? Include { get; set; }
        public string? IncludeAll { get; set; }
        public bool RunAlways { get; set; } = false;
        public bool RunOnChange { get; set; } = false;
        public int? Timeout { get; set; }
        public string? OnError { get; set; }
        public bool RequireContext { get; set; } = false;
        public string? ContextFilter { get; set; }
        public bool SplitStatements { get; set; } = true;
    }
}
