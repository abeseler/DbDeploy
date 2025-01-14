using DbDeploy.FileHandling;
using DbDeploy.Models;
using FluentAssertions;

namespace DbDeploy.Tests;

public sealed class SqlFileParserTests
{
    [Fact]
    public void Parse_ShouldReturnMigrations_WhenFileIsValid()
    {
        var file = new FileInfo("Migrations/Example.sql");
        var result = SqlFileParser.Parse(file, "Migrations/Example.sql", null, CancellationToken.None);
        var (migrations, exception) = result;

        result.Succeeded.Should().BeTrue();
        exception.Should().BeNull();
        migrations.Should().NotBeNullOrEmpty();
        migrations.Should().HaveCount(2);

        migrations![0].FileName.Should().Be("Migrations/Example.sql");
        migrations[0].Title.Should().Be("example:1");
        migrations[0].RunAlways.Should().BeFalse();
        migrations[0].RunOnChange.Should().BeFalse();
        migrations[0].RunInTransaction.Should().BeTrue();
        migrations[0].ContextRequired.Should().BeFalse();
        migrations[0].ContextFilter.Should().BeEmpty();
        migrations[0].Timeout.Should().Be(30);
        migrations[0].OnError.Should().Be(Migration.ErrorHandling.Fail);
        migrations[0].SqlStatements.Should().HaveCount(1);

        migrations[1].FileName.Should().Be("Migrations/Example.sql");
        migrations[1].Title.Should().Be("example:2");
        migrations[1].RunAlways.Should().BeTrue();
        migrations[1].RunOnChange.Should().BeTrue();
        migrations[1].RunInTransaction.Should().BeFalse();
        migrations[1].ContextRequired.Should().BeTrue();
        migrations[1].ContextFilter.Should().HaveCount(2);
        migrations[1].ContextFilter.Should().Contain("one");
        migrations[1].ContextFilter.Should().Contain("two");
        migrations[1].Timeout.Should().Be(42069);
        migrations[1].OnError.Should().Be(Migration.ErrorHandling.Skip);
        migrations[1].SqlStatements.Should().HaveCount(2);
    }
}
