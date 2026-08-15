using Ratchet.Commands;
using Xunit;

namespace Ratchet.Tests;

public sealed class DryRunCommandTests
{
    [Fact]
    public void ResolveOutputPath_UsesProcessCurrentDirectory_WhenPathIsRelative()
    {
        var expected = Path.GetFullPath("ratchet-plan.sql", Directory.GetCurrentDirectory());

        Assert.Equal(expected, DryRunCommand.ResolveOutputPath(null));
        Assert.Equal(expected, DryRunCommand.ResolveOutputPath(""));
        Assert.Equal(
            Path.GetFullPath(Path.Combine("reports", "plan.sql"), Directory.GetCurrentDirectory()),
            DryRunCommand.ResolveOutputPath(Path.Combine("reports", "plan.sql")));
    }

    [Fact]
    public void ResolveOutputPath_LeavesAbsolutePathsUnchanged()
    {
        var absolute = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ratchet-plan.sql"));

        Assert.Equal(absolute, DryRunCommand.ResolveOutputPath(absolute));
    }
}
