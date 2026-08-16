using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ratchet;
using Ratchet.Cli;
using Ratchet.Commands;
using Ratchet.Journal;
using Ratchet.Models;
using Ratchet.Parsing;
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

        Assert.Null(result);
    }

    [Fact]
    public async Task Execute_Fails_WhenStartingFileJsonIsInvalid()
    {
        WriteStartingFile("{ not json");

        var result = await Command().ExecuteAsync();

        Assert.NotNull(result);
        Assert.Contains("parsing starting file", result.Message, StringComparison.OrdinalIgnoreCase);
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

        Assert.NotNull(result);
        Assert.Contains("parse", result.Message, StringComparison.OrdinalIgnoreCase);
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

        Assert.NotNull(result);
        Assert.Contains("Tables/missing.sql", result.Message);
    }

    [Fact]
    public async Task Execute_Fails_WhenAppliedHashHasDrifted()
    {
        WriteStartingFile("""[{ "include": ["t.sql"] }]""");
        WriteSql("t.sql", "t");
        var dbPath = Path.Combine(_root, "app.db");
        var settings = SettingsFor(database: $"Data Source={dbPath}");
        var journal = new MigrationJournal(new SqliteDbProvider(settings.ConnectionString!), NullLogger<MigrationJournal>.Instance);
        await journal.EnsureTables();
        await journal.GetHistories();
        var applied = new Migration
        {
            FileName = "t.sql",
            Title = "t",
            SqlStatements = ["SELECT 1;"],
            Hash = "old-hash",
            ContextFilter = []
        };
        Assert.True((await journal.Apply(applied, null)).Succeeded);

        var command = Command(settings, journal);
        var result = await command.ExecuteAsync();

        Assert.NotNull(result);
        Assert.Contains("need repair", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_PassesWithDatabase_WhenHashesMatch()
    {
        WriteStartingFile("""[{ "include": ["t.sql"] }]""");
        WriteSql("t.sql", "t");
        var dbPath = Path.Combine(_root, "ok.db");
        var settings = SettingsFor(database: $"Data Source={dbPath}");
        var loader = new MigrationLoader(Options.Create(settings), NullLogger<MigrationLoader>.Instance);
        var journal = new MigrationJournal(new SqliteDbProvider(settings.ConnectionString!), NullLogger<MigrationJournal>.Instance);
        await journal.EnsureTables();
        await journal.GetHistories();
        var (parsed, error) = loader.Load(CancellationToken.None);
        Assert.Null(error);
        var migration = parsed!.Single();
        Assert.True((await journal.Apply(migration, null)).Succeeded);

        var result = await Command(settings, journal).ExecuteAsync();

        Assert.Null(result);
    }

    private ValidateCommand Command(Settings? settings = null, MigrationJournal? journal = null)
    {
        settings ??= SettingsFor(database: null);
        journal ??= new MigrationJournal(new UnconfiguredDbProvider(), NullLogger<MigrationJournal>.Instance);
        return new ValidateCommand(
            new MigrationLoader(Options.Create(settings), NullLogger<MigrationLoader>.Instance),
            journal,
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
            $$"""
            /* Migration { "title": "{{title}}" } */
            SELECT 1;
            """);
}
