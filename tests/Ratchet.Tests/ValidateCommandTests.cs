using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ratchet.Commands;
using Ratchet.Common;
using Ratchet.Data;
using Ratchet.FileHandling;
using Ratchet.Models;
using Xunit;

namespace Ratchet.Tests;

public sealed class ValidateCommandTests : IDisposable
{
    static ValidateCommandTests() => DefaultTypeMap.MatchNamesWithUnderscores = true;

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ratchet-validate-{Guid.NewGuid():N}");

    public ValidateCommandTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Execute_PassesFileOnly_WhenNoDatabaseIsConfigured()
    {
        WriteStartingFile("""[{ "include": ["ok.sql"] }]""");
        WriteSql("ok.sql", "ok:create");

        var result = await Command().ExecuteAsync();

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Execute_Fails_WhenStartingFileJsonIsInvalid()
    {
        WriteStartingFile("{ not json");

        var result = await Command().ExecuteAsync();

        Assert.True(result.Failed);
        Assert.Contains("parsing starting file", result.Match(_ => "", e => e.Message), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_Fails_WhenTitlesAreDuplicated()
    {
        WriteStartingFile("""[{ "include": ["dup.sql"] }]""");
        File.WriteAllText(Path.Combine(_root, "dup.sql"), """
            /* Migration
            { "title": "same" }
            */
            SELECT 1;
            /* Migration
            { "title": "SAME" }
            */
            SELECT 2;
            """);

        var result = await Command().ExecuteAsync();

        Assert.True(result.Failed);
        Assert.Contains("parse", result.Match(_ => "", e => e.Message), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_Fails_WhenDependsOnIsMissing()
    {
        WriteStartingFile("""[{ "include": ["fk.sql"] }]""");
        File.WriteAllText(Path.Combine(_root, "fk.sql"), """
            /* Migration
            { "title": "fk", "dependsOn": ["Tables/missing.sql"] }
            */
            SELECT 1;
            """);

        var result = await Command().ExecuteAsync();

        Assert.True(result.Failed);
        Assert.Contains("Tables/missing.sql", result.Match(_ => "", e => e.Message));
    }

    [Fact]
    public async Task Execute_Fails_WhenAppliedHashHasDrifted()
    {
        WriteStartingFile("""[{ "include": ["t.sql"] }]""");
        WriteSql("t.sql", "t");
        var dbPath = Path.Combine(_root, "app.db");
        var settings = SettingsFor(database: $"Data Source={dbPath}");
        var repo = new Repository(new SqliteDbProvider(settings.ConnectionString!), NullLogger<Repository>.Instance);
        await repo.EnsureMigrationTablesExist();
        await repo.GetAllMigrationHistories();
        var applied = new Migration
        {
            FileName = "t.sql",
            Title = "t",
            SqlStatements = ["SELECT 1;"],
            Hash = "old-hash",
            ContextFilter = []
        };
        Assert.True((await repo.ApplyMigration(applied, null)).Succeeded);

        var command = Command(settings, repo);
        var result = await command.ExecuteAsync();

        Assert.True(result.Failed);
        Assert.Contains("need repair", result.Match(_ => "", e => e.Message), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_PassesWithDatabase_WhenHashesMatch()
    {
        WriteStartingFile("""[{ "include": ["t.sql"] }]""");
        WriteSql("t.sql", "t");
        var dbPath = Path.Combine(_root, "ok.db");
        var settings = SettingsFor(database: $"Data Source={dbPath}");
        var extractor = new FileMigrationExtractor(Options.Create(settings), NullLogger<FileMigrationExtractor>.Instance);
        var repo = new Repository(new SqliteDbProvider(settings.ConnectionString!), NullLogger<Repository>.Instance);
        await repo.EnsureMigrationTablesExist();
        await repo.GetAllMigrationHistories();
        var (parsed, error) = extractor.ExtractFromStartingFile(CancellationToken.None);
        Assert.Null(error);
        var migration = parsed!.Values.Single();
        Assert.True((await repo.ApplyMigration(migration, null)).Succeeded);

        var result = await Command(settings, repo).ExecuteAsync();

        Assert.True(result.Succeeded);
    }

    private ValidateCommand Command(Settings? settings = null, Repository? repo = null)
    {
        settings ??= SettingsFor(database: null);
        repo ??= new Repository(new UnconfiguredDbProvider(), NullLogger<Repository>.Instance);
        return new ValidateCommand(
            new FileMigrationExtractor(Options.Create(settings), NullLogger<FileMigrationExtractor>.Instance),
            repo,
            Options.Create(settings),
            NullLogger<ValidateCommand>.Instance);
    }

    private Settings SettingsFor(string? database) => new()
    {
        WorkingDirectory = _root,
        StartingFile = "ratchet.json",
        DatabaseProvider = database is null ? Settings.DefaultDatabaseProvider : "sqlite",
        ConnectionString = database
    };

    private void WriteStartingFile(string json) =>
        File.WriteAllText(Path.Combine(_root, "ratchet.json"), json);

    private void WriteSql(string name, string title) =>
        File.WriteAllText(Path.Combine(_root, name),
            "/* Migration\n{\n    \"title\": \"" + title + "\"\n}\n*/\nSELECT 1;\n");
}
