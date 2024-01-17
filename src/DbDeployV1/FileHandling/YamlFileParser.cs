using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace DbDeploy.FileHandling;

internal static class YamlFileParser
{
    public static Result<List<Migration>, Exception> Parse(string text, string fileName)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        var migrationsYamls = deserializer.Deserialize<List<MigrationYaml>>(text);
        var migrations = new List<Migration>();

        foreach (var migrationYaml in migrationsYamls)
        {
            var migration = new Migration
            {
                FileName = fileName,
                Title = migrationYaml.Title,
                Include = migrationYaml.Include,
                IncludeAll = migrationYaml.IncludeAll,
                SqlStatements = migrationYaml.Sql is not null ? SqlFileParser.ParseSqlStatements(migrationYaml.Sql, migrationYaml.SplitStatements) : [],
                RunAlways = migrationYaml.RunAlways,
                RunOnChange = migrationYaml.RunOnChange,
                Timeout = migrationYaml.Timeout,
                OnError = Enum.TryParse<Migration.ErrorHandling>(migrationYaml.OnError, out var onError) ? onError : Migration.ErrorHandling.Fail,
                RequireContext = migrationYaml.RequireContext,
                ContextFilter = migrationYaml.ContextFilter?.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray() ?? []
            };

            if (migration.Type == Migration.MigrationType.Sql && migrations.Any(x => x.Title == migration.Title))
                return new ValidationException($"File '{fileName}' has a duplicate migration title: {migration.Title}");

            if (migration.Type == Migration.MigrationType.Sql)
                migration.Hash = Migration.CalculateHash(fileName, migrationYaml.Title!, migrationYaml.Sql!);

            migrations.Add(migration);
        }

        return migrations;
    }

    private sealed class MigrationYaml
    {
        [YamlMember(Alias = "title")]
        public string? Title { get; set; }

        [YamlMember(Alias = "sql")]
        public string? Sql { get; set; }

        [YamlMember(Alias = "include")]
        public string? Include { get; set; }

        [YamlMember(Alias = "include-all")]
        public string? IncludeAll { get; set; }

        [YamlMember(Alias = "run-always")]
        public bool RunAlways { get; set; } = false;

        [YamlMember(Alias = "run-on-change")]
        public bool RunOnChange { get; set; } = false;

        [YamlMember(Alias = "timeout")]
        public int? Timeout { get; set; }

        [YamlMember(Alias = "on-error")]
        public string? OnError { get; set; }

        [YamlMember(Alias = "require-context")]
        public bool RequireContext { get; set; } = false;

        [YamlMember(Alias = "context-filter")]
        public string? ContextFilter { get; set; }

        [YamlMember(Alias = "split-statements")]
        public bool SplitStatements { get; set; } = true;
    }
}
