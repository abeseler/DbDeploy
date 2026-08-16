using Xunit;
using Ratchet.Parsing;

namespace Ratchet.Tests;

public sealed class SqlFileTokensTests
{
    [Theory]
    [InlineData("/* Migration")]
    [InlineData("/* Migration {")]
    [InlineData("/* Migration { \"title\": \"x\" } */")]
    [InlineData("/* migration {")]
    [InlineData("  /* Migration {")]
    [InlineData("/* Migration{")]
    [InlineData("/* Migration   ")]
    public void TryStartHeader_Accepts(string line)
    {
        Assert.True(SqlFileTokens.TryStartHeader(line, out _));
    }

    [Theory]
    [InlineData("/* Migration notes: backfilled */")]
    [InlineData("/* Migrations { \"title\": \"nope\" } */")]
    [InlineData("/* Migration */")]
    [InlineData("/*Migration {")]
    [InlineData("-- Migration {")]
    [InlineData("SELECT 1;")]
    public void TryStartHeader_Rejects(string line)
    {
        Assert.False(SqlFileTokens.TryStartHeader(line, out var remainder));
        Assert.Equal("", remainder);
    }

    [Fact]
    public void TryStartHeader_ReturnsRemainderAfterTheMarker()
    {
        Assert.True(SqlFileTokens.TryStartHeader("/* Migration { \"title\": \"x\" } */", out var remainder));
        Assert.Equal(" { \"title\": \"x\" } */", remainder);
    }

    [Theory]
    [InlineData("--NewStatement")]
    [InlineData("-- NewStatement")]
    [InlineData("--  NewStatement")]
    [InlineData("    -- NewStatement")]
    [InlineData("-- newstatement")]
    [InlineData("-- NewStatement  seed batch two")]
    [InlineData("--NewStatement\tnote")]
    public void IsStatementSeparator_Accepts(string line)
    {
        Assert.True(SqlFileTokens.IsStatementSeparator(line));
    }

    [Theory]
    [InlineData("--NewStatementFoo")]
    [InlineData("-- NotAStatement")]
    [InlineData("-- New Statement")]
    [InlineData("SELECT 1;")]
    [InlineData("")]
    public void IsStatementSeparator_Rejects(string line)
    {
        Assert.False(SqlFileTokens.IsStatementSeparator(line));
    }
}
