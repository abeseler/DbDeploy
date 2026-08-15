using Ratchet.Commands;
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

    [Theory]
    [InlineData("--version")]
    [InlineData("--VERSION")]
    public void IsVersionRequest_RecognizesVersionFlag(string flag) =>
        Assert.True(Usage.IsVersionRequest(["--command", "update", flag]));

    [Fact]
    public void IsVersionRequest_IsFalseForNormalArgs() =>
        Assert.False(Usage.IsVersionRequest(["--command", "update"]));

    [Theory]
    [InlineData("version")]
    [InlineData("VERSION")]
    public void IsVersionCommand_RecognizesVersion(string command) =>
        Assert.True(Usage.IsVersionCommand(command));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("update")]
    [InlineData("help")]
    public void IsVersionCommand_IsFalseOtherwise(string? command) =>
        Assert.False(Usage.IsVersionCommand(command));

    [Fact]
    public void Text_ListsEveryRegisteredCommand()
    {
        foreach (var command in CommandNames.All)
            Assert.Contains(command, Usage.Text);
    }

    [Fact]
    public void Text_ListsVersionFlag() =>
        Assert.Contains("--version", Usage.Text);
}
