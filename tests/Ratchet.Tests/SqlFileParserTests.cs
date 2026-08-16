using Xunit;
using Ratchet;
using Ratchet.Models;
using Ratchet.Parsing;

namespace Ratchet.Tests;

public sealed class SqlFileParserTests
{
    [Fact]
    public void Parse_ReadsTwoBlocks_WithMixedHeadersAndASeparator()
    {
        var migrations = ParseAll("""
            /* Migration { "title": "example:1" } */
            CREATE TABLE example (
                id INT GENERATED ALWAYS AS IDENTITY,
                created_on TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
                CONSTRAINT pk_example PRIMARY KEY (id)
            );

            /* Migration
            {
                "title": "example:2",
                "run": "always",
                "runInTransaction": false,
                "contextFilter": ["one", "two"],
                "contextRequired": true,
                "timeout": 42069,
                "onError": "Skip"
            }
            */
            ALTER TABLE example
            ADD column_one TEXT NULL;

            --NewStatement

            ALTER TABLE example
            ADD column_two TEXT NULL;
            """, "example.sql");

        Assert.Equal(2, migrations.Count);

        var first = migrations[0];
        Assert.Equal("example.sql", first.FileName);
        Assert.Equal("example:1", first.Title);
        Assert.Equal(Migration.RunMode.Once, first.Run);
        Assert.True(first.RunInTransaction);
        Assert.False(first.ContextRequired);
        Assert.Empty(first.ContextFilter);
        Assert.Equal(30, first.Timeout);
        Assert.Equal(Migration.ErrorHandling.Fail, first.OnError);
        Assert.Single(first.SqlStatements);
        Assert.Contains("CREATE TABLE example", first.SqlStatements[0], StringComparison.Ordinal);

        var second = migrations[1];
        Assert.Equal("example:2", second.Title);
        Assert.Equal(Migration.RunMode.Always, second.Run);
        Assert.False(second.RunInTransaction);
        Assert.True(second.ContextRequired);
        Assert.Equal(["one", "two"], second.ContextFilter);
        Assert.Equal(42069, second.Timeout);
        Assert.Equal(Migration.ErrorHandling.Skip, second.OnError);
        Assert.Equal(2, second.SqlStatements.Length);
        Assert.Contains("column_one", second.SqlStatements[0], StringComparison.Ordinal);
        Assert.Contains("column_two", second.SqlStatements[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ReadsAOneLineHeader()
    {
        var migration = ParseOne("""
            /* Migration { "title": "widget:createTable" } */
            CREATE TABLE widget (id INT);
            """);

        Assert.Equal("widget:createTable", migration.Title);
        Assert.Equal(Migration.RunMode.Once, migration.Run);
        Assert.Contains("CREATE TABLE widget", migration.SqlStatements[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ReadsAOneLineHeader_WhenClosingTokenHasTrailingWhitespace()
    {
        var migration = ParseOne("""
            /* Migration { "title": "one" } */   
            SELECT 1;
            """);

        Assert.Equal("one", migration.Title);
    }

    [Fact]
    public void Parse_ReadsAHeader_WhenBraceIsOnTheOpenerLine()
    {
        var migration = ParseOne("""
            /* Migration {
            "title": "abc" } */
            SELECT 1;
            """);

        Assert.Equal("abc", migration.Title);
    }

    [Fact]
    public void Parse_ReadsAnIndentedHeader()
    {
        var migration = ParseOne("""
              /* Migration { "title": "indented" } */
            SELECT 1;
            """);

        Assert.Equal("indented", migration.Title);
    }

    [Fact]
    public void Parse_UsesTheRelativePathAsTheFileName()
    {
        var migration = ParseOne("""
            /* Migration { "title": "x" } */
            SELECT 1;
            """, "Tables/widget.sql");

        Assert.Equal("Tables/widget.sql", migration.FileName);
        Assert.Equal("Tables/widget.sql [x]", migration.Id);
    }

    [Fact]
    public void Parse_ReadsDependsOnAndOnErrorMark()
    {
        var migration = ParseOne("""
            /* Migration { "title": "fk", "dependsOn": ["Tables/orders.sql"], "onError": "Mark" } */
            SELECT 1;
            """);

        Assert.Equal(["Tables/orders.sql"], migration.DependsOn);
        Assert.Equal(Migration.ErrorHandling.Mark, migration.OnError);
    }

    [Fact]
    public void Parse_MergesOptionsContextOntoTheMigration()
    {
        var options = new ParseOptions
        {
            ContextFilter = ["seed"],
            ContextRequired = true
        };

        var migration = ParseOne("""
            /* Migration { "title": "x", "contextFilter": ["one"] } */
            SELECT 1;
            """, options: options);

        Assert.True(migration.ContextRequired);
        Assert.Equal(["one", "seed"], migration.ContextFilter);
    }

    [Fact]
    public void Parse_ReadsRunOnChange()
    {
        var migration = ParseOne("""
            /* Migration
            { "title": "vw", "run": "onChange" }
            */
            SELECT 1;
            """);

        Assert.Equal(Migration.RunMode.OnChange, migration.Run);
    }

    [Fact]
    public void Parse_ReadsRunNever()
    {
        var migration = ParseOne("""
            /* Migration
            { "title": "parked", "run": "never" }
            */
            SELECT 1;
            """);

        Assert.Equal(Migration.RunMode.Never, migration.Run);
    }

    [Fact]
    public void Parse_RejectsUnknownRunValue()
    {
        var result = Parse("""
            /* Migration
            { "title": "vw", "run": "sometimes" }
            */
            SELECT 1;
            """);

        Assert.True(result.Failed);
        Assert.Contains("sometimes", result.Match(_ => "", e => e.Message));
    }

    [Fact]
    public void Parse_RejectsReplacedRunFlags()
    {
        var result = Parse("""
            /* Migration
            { "title": "seed", "runAlways": true }
            */
            SELECT 1;
            """);

        Assert.True(result.Failed);
        Assert.Contains("run:", result.Match(_ => "", e => e.Message), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_RejectsInvalidHeaderJson()
    {
        var result = Parse("""
            /* Migration { title: missing-quotes } */
            SELECT 1;
            """);

        Assert.True(result.Failed);
        Assert.NotEqual(Errors.FileIsEmpty.Message, result.Match(_ => "", e => e.Message));
    }

    [Fact]
    public void Parse_ReturnsUnclosedHeader_WhenTheBlockCommentNeverCloses()
    {
        var result = Parse("""
            /* Migration
            { "title": "open"
            SELECT 1;
            """);

        Assert.True(result.Failed);
        Assert.Contains("Unclosed migration header", result.Match(_ => "", e => e.Message));
    }

    [Fact]
    public void Parse_DoesNotTreatProseMigrationCommentAsAHeader()
    {
        var result = Parse("""
            /* Migration notes: backfilled customer_id */
            SELECT 1;
            """);

        Assert.True(result.Failed);
        Assert.Equal(Errors.FileIsEmpty.Message, result.Match(_ => "", e => e.Message));
    }

    [Fact]
    public void Parse_DoesNotTreatMigrationsPluralAsAHeader()
    {
        var result = Parse("""
            /* Migrations { "title": "nope" } */
            SELECT 1;
            """);

        Assert.True(result.Failed);
        Assert.Equal(Errors.FileIsEmpty.Message, result.Match(_ => "", e => e.Message));
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenHeaderHasNoTitle()
    {
        var result = Parse("""
            /* Migration { "run": "once" } */
            SELECT 1;
            """);

        Assert.True(result.Failed);
        Assert.Equal(Errors.FileIsEmpty.Message, result.Match(_ => "", e => e.Message));
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenHeaderHasNoSql()
    {
        var result = Parse("""
            /* Migration { "title": "lonely" } */
            """);

        Assert.True(result.Failed);
        Assert.Equal(Errors.FileIsEmpty.Message, result.Match(_ => "", e => e.Message));
    }

    [Fact]
    public void Parse_DiscardsSqlThatAppearsBeforeTheFirstHeader()
    {
        var migration = ParseOne("""
            SELECT 0;
            /* Migration { "title": "kept" } */
            SELECT 1;
            """);

        Assert.Equal("kept", migration.Title);
        Assert.DoesNotContain("SELECT 0;", migration.SqlStatements[0], StringComparison.Ordinal);
        Assert.Contains("SELECT 1;", migration.SqlStatements[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_DiscardsAHeaderWithNoSql_WhenALaterBlockIsValid()
    {
        var migrations = ParseAll("""
            /* Migration { "title": "dropped" } */
            /* Migration { "title": "kept" } */
            SELECT 1;
            """);

        Assert.Single(migrations);
        Assert.Equal("kept", migrations[0].Title);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenTheFileHasNoContent()
    {
        var result = Parse("");

        Assert.True(result.Failed);
        Assert.Equal(Errors.FileIsEmpty.Message, result.Match(_ => "", e => e.Message));
    }

    [Fact]
    public void Parse_ReturnsEmptyList_WhenTheFileHasNoMigrationsAndMissingIsAllowed()
    {
        var options = new ParseOptions { ErrorIfMissingOrEmpty = false };
        var result = Parse("-- just a comment", options: options);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Match(m => m!, _ => []));
    }

    [Fact]
    public void Parse_SplitsOnSpacedNewStatement()
    {
        var statements = ParseStatements("""
            /* Migration { "title": "split" } */
            SELECT 1;
            -- NewStatement
            SELECT 2;
            """);

        Assert.Equal(2, statements.Length);
        Assert.Contains("SELECT 1;", statements[0], StringComparison.Ordinal);
        Assert.Contains("SELECT 2;", statements[1], StringComparison.Ordinal);
        Assert.DoesNotContain("NewStatement", string.Join('\n', statements), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_SplitsOnJammedNewStatement()
    {
        var statements = ParseStatements("""
            /* Migration { "title": "split" } */
            SELECT 1;
            --NewStatement
            SELECT 2;
            """);

        Assert.Equal(2, statements.Length);
        Assert.Contains("SELECT 1;", statements[0], StringComparison.Ordinal);
        Assert.Contains("SELECT 2;", statements[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_SplitsOnIndentedNewStatementWithANote()
    {
        var statements = ParseStatements("""
            /* Migration { "title": "split" } */
            SELECT 1;
                -- NewStatement  seed batch two
            SELECT 2;
            """);

        Assert.Equal(2, statements.Length);
        Assert.Contains("SELECT 1;", statements[0], StringComparison.Ordinal);
        Assert.Contains("SELECT 2;", statements[1], StringComparison.Ordinal);
        Assert.DoesNotContain("seed batch two", string.Join('\n', statements), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_DoesNotSplitOnNewStatementFoo()
    {
        var statements = ParseStatements("""
            /* Migration { "title": "one" } */
            SELECT 1;
            --NewStatementFoo
            SELECT 2;
            """);

        Assert.Single(statements);
        Assert.Contains("SELECT 1;", statements[0], StringComparison.Ordinal);
        Assert.Contains("--NewStatementFoo", statements[0], StringComparison.Ordinal);
        Assert.Contains("SELECT 2;", statements[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_IgnoresConsecutiveAndEdgeSeparators()
    {
        var statements = ParseStatements("""
            /* Migration { "title": "split" } */
            -- NewStatement
            SELECT 1;

            -- NewStatement
            -- NewStatement
            SELECT 2;
            -- NewStatement
            """);

        Assert.Equal(2, statements.Length);
        Assert.Contains("SELECT 1;", statements[0], StringComparison.Ordinal);
        Assert.Contains("SELECT 2;", statements[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_DoesNotTreatBlankLinesAsStatements()
    {
        var statements = ParseStatements("""
            /* Migration { "title": "one" } */
            SELECT 1;

            SELECT 2;
            """);

        Assert.Single(statements);
        Assert.Contains("SELECT 1;", statements[0], StringComparison.Ordinal);
        Assert.Contains("SELECT 2;", statements[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_AllowsDistinctTitlesInTheSameFile()
    {
        var migrations = ParseAll("""
            /* Migration
            { "title": "orders:create" }
            */
            SELECT 1;

            /* Migration
            { "title": "orders:addColumn" }
            */
            SELECT 2;
            """);

        Assert.Equal(2, migrations.Count);
        Assert.Equal("orders:create", migrations[0].Title);
        Assert.Equal("orders:addColumn", migrations[1].Title);
    }

    [Fact]
    public void Parse_ReturnsDuplicateTitle_WhenTitlesMatchExactly()
    {
        var result = Parse("""
            /* Migration
            { "title": "orders:create" }
            */
            SELECT 1;

            /* Migration
            { "title": "orders:create" }
            */
            SELECT 2;
            """);

        Assert.True(result.Failed);
        Assert.Contains("orders:create", result.Match(_ => "", e => e.Message));
    }

    [Fact]
    public void Parse_ReturnsDuplicateTitle_WhenTitlesDifferOnlyByCase()
    {
        var result = Parse("""
            /* Migration
            { "title": "orders:create" }
            */
            SELECT 1;

            /* Migration
            { "title": "Orders:Create" }
            */
            SELECT 2;
            """);

        Assert.True(result.Failed);
        Assert.Contains("Orders:Create", result.Match(_ => "", e => e.Message));
    }

    [Fact]
    public void Parse_Throws_WhenTheTokenIsAlreadyCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => SqlFileParser.Parse("""
            /* Migration { "title": "x" } */
            SELECT 1;
            """, "temp.sql", stoppingToken: cts.Token));
    }

    private static string[] ParseStatements(string sql) => ParseOne(sql).SqlStatements;

    private static Migration ParseOne(string sql, string path = "temp.sql", ParseOptions? options = null)
    {
        var migrations = ParseAll(sql, path, options);
        Assert.Single(migrations);
        return migrations[0];
    }

    private static List<Migration> ParseAll(string sql, string path = "temp.sql", ParseOptions? options = null)
    {
        var result = Parse(sql, path, options);
        Assert.True(result.Succeeded, result.Match(_ => "", e => e.Message));
        return result.Match(m => m!, _ => []);
    }

    private static Result<List<Migration>> Parse(string sql, string path = "temp.sql", ParseOptions? options = null) =>
        SqlFileParser.Parse(sql, path, options);
}
