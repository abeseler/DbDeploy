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
        var pendingBaseline = History("legacy.sql", "legacy", hash: null);
        var histories = new Dictionary<string, MigrationHistory>
        {
            [applied.MigrationId] = applied,
            [drifted.MigrationId] = drifted,
            [pendingBaseline.MigrationId] = pendingBaseline
        };

        var migrations = new List<Migration>
        {
            New("seed.sql", "seed", contextFilter: ["prod"]),
            New("keep.sql", "keep", hash: "keep"),
            New("drift.sql", "drift", hash: "new"),
            New("legacy.sql", "legacy", hash: "legacy"),
            New("new.sql", "new", hash: "new")
        };

        var plan = DeploymentPlanner.Build(migrations, histories, ["dev"]);

        Assert.Equal(["seed.sql [seed]"], plan.FilteredOut.Select(m => m.Id));
        Assert.Equal(["keep.sql [keep]", "drift.sql [drift]", "legacy.sql [legacy]", "new.sql [new]"], plan.Resolved.Select(m => m.Id));
        Assert.Equal(["legacy.sql [legacy]", "new.sql [new]"], plan.ToBaseline.Select(p => p.Migration.Id));
        Assert.Equal(["legacy.sql [legacy]"], plan.PendingBaseline.Select(p => p.Migration.Id));
        Assert.Equal(["drift.sql [drift]"], plan.ToRepair.Select(p => p.Migration.Id));
        Assert.Equal(["new.sql [new]"], plan.ToApply.Select(p => p.Migration.Id));
        Assert.Equal(1, plan.UpToDateCount);
        Assert.Equal(3, plan.HistoryCount);
    }

    [Fact]
    public void Build_QueuesRunOnChangeWhenHashDiffers()
    {
        var history = History("view.sql", "view", hash: "old");
        var migration = New("view.sql", "view", hash: "new", runOnChange: true);

        var plan = DeploymentPlanner.Build([migration], new Dictionary<string, MigrationHistory> { [history.MigrationId] = history }, []);

        Assert.Empty(plan.ToRepair);
        Assert.Equal(["view.sql [view]"], plan.ToApply.Select(p => p.Migration.Id));
        Assert.Same(history, plan.ToApply[0].History);
    }

    [Fact]
    public void Build_QueuesRunAlwaysEvenWhenHashMatches()
    {
        var history = History("seed.sql", "seed", hash: "same");
        var migration = New("seed.sql", "seed", hash: "same", runAlways: true);

        var plan = DeploymentPlanner.Build([migration], new Dictionary<string, MigrationHistory> { [history.MigrationId] = history }, []);

        Assert.Empty(plan.ToRepair);
        Assert.Equal(["seed.sql [seed]"], plan.ToApply.Select(p => p.Migration.Id));
    }

    [Fact]
    public void Prepare_OrdersThenClassifies_UsingTheSameHistories()
    {
        var appliedDep = History("Tables/orders.sql", "orders", hash: "orders");
        var histories = new Dictionary<string, MigrationHistory> { [appliedDep.MigrationId] = appliedDep };
        var migrations = new List<Migration>
        {
            New("Fks/fk.sql", "fk", hash: "fk", dependsOn: ["Tables/orders.sql"]),
            New("Tables/orders.sql", "orders", hash: "orders")
        };

        var (plan, error) = DeploymentPlanner.Prepare(migrations, histories, []);

        Assert.Null(error);
        Assert.NotNull(plan);
        Assert.Equal(["Tables/orders.sql [orders]", "Fks/fk.sql [fk]"], plan!.Resolved.Select(m => m.Id));
        Assert.Equal(["Fks/fk.sql [fk]"], plan.ToApply.Select(p => p.Migration.Id));
        Assert.Empty(plan.ToRepair);
    }

    [Fact]
    public void Build_DoesNotTreatMatchingHashAsPending()
    {
        var history = History("t.sql", "t", hash: "abc");
        var migration = New("t.sql", "t", hash: "abc");

        var plan = DeploymentPlanner.Build([migration], new Dictionary<string, MigrationHistory> { [history.MigrationId] = history }, []);

        Assert.Empty(plan.ToApply);
        Assert.Empty(plan.ToBaseline);
        Assert.Empty(plan.ToRepair);
        Assert.Empty(plan.FilteredOut);
        Assert.Equal(["t.sql [t]"], plan.Resolved.Select(m => m.Id));
    }

    private static Migration New(string file, string title, string hash = "h", string[]? contextFilter = null, bool runOnChange = false, bool runAlways = false, string[]? dependsOn = null) => new()
    {
        FileName = file,
        Title = title,
        SqlStatements = ["select 1;"],
        Hash = hash,
        ContextFilter = contextFilter ?? [],
        DependsOn = dependsOn ?? [],
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
