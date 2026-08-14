using Ratchet.Models;
using Xunit;

namespace Ratchet.Tests;

public sealed class DeploymentPlannerTests
{
    [Fact]
    public void Build_ClassifiesContextFilterHashNullInvalidAndPendingApply()
    {
        var applied = History("keep.sql", "keep", hash: "keep");
        var drifted = History("drift.sql", "drift", hash: "old");
        var pendingSync = History("sync.sql", "sync", hash: null);
        var histories = new Dictionary<string, MigrationHistory>
        {
            [applied.MigrationId] = applied,
            [drifted.MigrationId] = drifted,
            [pendingSync.MigrationId] = pendingSync
        };

        var migrations = new List<Migration>
        {
            New("seed.sql", "seed", contextFilter: ["prod"]),
            New("keep.sql", "keep", hash: "keep"),
            New("drift.sql", "drift", hash: "new"),
            New("sync.sql", "sync", hash: "sync"),
            New("new.sql", "new", hash: "new")
        };

        var plan = DeploymentPlanner.Build(migrations, histories, ["dev"]);

        Assert.Equal(["seed.sql [seed]"], plan.FilteredOut.Select(m => m.Id));
        Assert.Equal(["keep.sql [keep]", "drift.sql [drift]", "sync.sql [sync]", "new.sql [new]"], plan.Resolved.Select(m => m.Id));
        Assert.Equal(["sync.sql [sync]"], plan.ToSync.Select(p => p.Migration.Id));
        Assert.Equal(["drift.sql [drift]"], plan.InvalidChanges.Select(m => m.Id));
        Assert.Equal(["new.sql [new]"], plan.ToApply.Select(p => p.Migration.Id));
        Assert.Equal(3, plan.HistoryCount);
    }

    [Fact]
    public void Build_QueuesRunOnChangeWhenHashDiffers()
    {
        var history = History("view.sql", "view", hash: "old");
        var migration = New("view.sql", "view", hash: "new", runOnChange: true);

        var plan = DeploymentPlanner.Build([migration], new Dictionary<string, MigrationHistory> { [history.MigrationId] = history }, []);

        Assert.Empty(plan.InvalidChanges);
        Assert.Equal(["view.sql [view]"], plan.ToApply.Select(p => p.Migration.Id));
        Assert.Same(history, plan.ToApply[0].History);
    }

    [Fact]
    public void Build_QueuesRunAlwaysEvenWhenHashMatches()
    {
        var history = History("seed.sql", "seed", hash: "same");
        var migration = New("seed.sql", "seed", hash: "same", runAlways: true);

        var plan = DeploymentPlanner.Build([migration], new Dictionary<string, MigrationHistory> { [history.MigrationId] = history }, []);

        Assert.Empty(plan.InvalidChanges);
        Assert.Equal(["seed.sql [seed]"], plan.ToApply.Select(p => p.Migration.Id));
    }

    [Fact]
    public void Build_DoesNotTreatMatchingHashAsPending()
    {
        var history = History("t.sql", "t", hash: "abc");
        var migration = New("t.sql", "t", hash: "abc");

        var plan = DeploymentPlanner.Build([migration], new Dictionary<string, MigrationHistory> { [history.MigrationId] = history }, []);

        Assert.Empty(plan.ToApply);
        Assert.Empty(plan.ToSync);
        Assert.Empty(plan.InvalidChanges);
        Assert.Empty(plan.FilteredOut);
        Assert.Equal(["t.sql [t]"], plan.Resolved.Select(m => m.Id));
    }

    private static Migration New(string file, string title, string hash = "h", string[]? contextFilter = null, bool runOnChange = false, bool runAlways = false) => new()
    {
        FileName = file,
        Title = title,
        SqlStatements = ["select 1;"],
        Hash = hash,
        ContextFilter = contextFilter ?? [],
        RunOnChange = runOnChange,
        RunAlways = runAlways
    };

    private static MigrationHistory History(string file, string title, string? hash) => new()
    {
        FileName = file,
        Title = title,
        Hash = hash
    };
}
