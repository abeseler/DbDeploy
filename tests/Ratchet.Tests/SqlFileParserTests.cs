using Xunit;
using Ratchet.FileHandling;
using Ratchet.Models;

namespace Ratchet.Tests;

public sealed class SqlFileParserTests
{
    [Fact]
    public void Parse_ShouldReturnMigrations_WhenFileIsValid()
    {
        var file = new FileInfo("Migrations/Example.sql");
        var result = SqlFileParser.Parse(file, "Migrations/Example.sql", null, CancellationToken.None);
        var (migrations, exception) = result;

        Assert.True(result.Succeeded);
        Assert.Null(exception);
        Assert.NotNull(migrations);
        Assert.Equal(2, migrations!.Count);

        var migration = migrations![0];

        Assert.Equal("Migrations/Example.sql", migration.FileName);
        Assert.Equal("example:1", migration.Title);
        Assert.False(migration.RunAlways);
        Assert.False(migration.RunOnChange);
        Assert.True(migration.RunInTransaction);
        Assert.False(migration.ContextRequired);
        Assert.Empty(migration.ContextFilter);
        Assert.Equal(30, migration.Timeout);
        Assert.Equal(Migration.ErrorHandling.Fail, migration.OnError);
        Assert.Single(migration.SqlStatements);

        migration = migrations[1];

        Assert.Equal("Migrations/Example.sql", migration.FileName);
        Assert.Equal("example:2", migration.Title);
        Assert.True(migration.RunAlways);
        Assert.True(migration.RunOnChange);
        Assert.False(migration.RunInTransaction);
        Assert.True(migration.ContextRequired);
        Assert.Equal(2, migration.ContextFilter.Length);
        Assert.Contains("one", migration.ContextFilter);
        Assert.Contains("two", migration.ContextFilter);
        Assert.Equal(42069, migration.Timeout);
        Assert.Equal(Migration.ErrorHandling.Skip, migration.OnError);
        Assert.Equal(2, migration.SqlStatements.Length);
    }

    [Fact]
    public void Parse_ReturnsDuplicateTitle_WhenTitlesMatchExactly()
    {
        var result = ParseTemporary("""
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
        var result = ParseTemporary("""
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
    public void Parse_AllowsDistinctTitlesInTheSameFile()
    {
        var result = ParseTemporary("""
            /* Migration
            { "title": "orders:create" }
            */
            SELECT 1;

            /* Migration
            { "title": "orders:addColumn" }
            */
            SELECT 2;
            """);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Match(m => m!.Count, _ => 0));
    }

    private static Ratchet.Common.Result<List<Migration>> ParseTemporary(string sql)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ratchet-parse-{Guid.NewGuid():N}.sql");
        try
        {
            File.WriteAllText(path, sql);
            return SqlFileParser.Parse(new FileInfo(path), "temp.sql", null, CancellationToken.None);
        }
        finally
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }
}
