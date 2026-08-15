using Ratchet.Common;
using Xunit;

namespace Ratchet.Tests;

public sealed class ArgumentsTests
{
    [Fact]
    public void Peel_TakesTheFirstTokenAsTheCommand()
    {
        var parsed = Arguments.Peel(["update", "--provider", "postgres"]);

        Assert.Equal("update", parsed.PositionalCommand);
        Assert.Equal(["--provider", "postgres"], parsed.Remaining);
        Assert.False(parsed.CommandSpecifiedTwice);
    }

    [Fact]
    public void Peel_LeavesFlagFirstArgvAlone()
    {
        var parsed = Arguments.Peel(["--command", "status", "--provider", "sqlite"]);

        Assert.Null(parsed.PositionalCommand);
        Assert.Equal(["--command", "status", "--provider", "sqlite"], parsed.Remaining);
        Assert.False(parsed.CommandSpecifiedTwice);
    }

    [Fact]
    public void Peel_DetectsCommandFlagAfterSubcommand()
    {
        var parsed = Arguments.Peel(["update", "--command", "status"]);

        Assert.Equal("update", parsed.PositionalCommand);
        Assert.True(parsed.CommandSpecifiedTwice);
    }

    [Fact]
    public void Peel_DetectsCommandEqualsFormAfterSubcommand()
    {
        var parsed = Arguments.Peel(["update", "--command=status"]);

        Assert.True(parsed.CommandSpecifiedTwice);
    }

    [Fact]
    public void Peel_EmptyArgvHasNoCommand()
    {
        var parsed = Arguments.Peel([]);

        Assert.Null(parsed.PositionalCommand);
        Assert.Empty(parsed.Remaining);
        Assert.False(parsed.CommandSpecifiedTwice);
    }
}
