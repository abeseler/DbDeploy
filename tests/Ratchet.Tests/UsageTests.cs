using Ratchet.Common;
using Xunit;

namespace Ratchet.Tests;

public sealed class UsageTests
{
    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("-?")]
    [InlineData("/?")]
    [InlineData("--HELP")]
    public void IsHelpRequest_RecognizesHelpFlags(string flag) =>
        Assert.True(Usage.IsHelpRequest(["--command", "update", flag]));

    [Fact]
    public void IsHelpRequest_IsFalseForNormalArgs() =>
        Assert.False(Usage.IsHelpRequest(["--command", "update"]));

    [Theory]
    [InlineData("help")]
    [InlineData("HELP")]
    public void IsHelpCommand_RecognizesHelp(string command) =>
        Assert.True(Usage.IsHelpCommand(command));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("update")]
    public void IsHelpCommand_IsFalseOtherwise(string? command) =>
        Assert.False(Usage.IsHelpCommand(command));
}
