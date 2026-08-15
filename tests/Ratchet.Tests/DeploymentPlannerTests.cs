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
        var migration = New("view.sql", "view", hash: "new", run: Migration.RunMode.OnChange);

        var plan = DeploymentPlanner.Build([migration], new Dictionary<string, MigrationHistory> { [history.MigrationId] = history }, []);

        Assert.Empty(plan.ToRepair);
        Assert.Equal(["view.sql [view]"], plan.ToApply.Select(p => p.Migration.Id));
        Assert.Same(history, plan.ToApply[0].History);
    }

    [Fact]
    public void Build_QueuesRunAlwaysEvenWhenHashMatches()
    {
        var history = History("seed.sql", "seed", hash: "same");
        var migration = New("seed.sql", "seed", hash: "same", run: Migration.RunMode.Always);

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
        Assert.Empty(plan.Ignored);
        Assert.Equal(["t.sql [t]"], plan.Resolved.Select(m => m.Id));
    }

    [Fact]
    public void Build_IgnoresNever_EvenWhenHashDriftedOrMissing()
    {
        var drifted = History("parked.sql", "parked", hash: "old");
        var histories = new Dictionary<string, MigrationHistory> { [drifted.MigrationId] = drifted };
        var parked = New("parked.sql", "parked", hash: "new", run: Migration.RunMode.Never);
        var fresh = New("fresh.sql", "fresh", hash: "fresh", run: Migration.RunMode.Never);

        var plan = DeploymentPlanner.Build([parked, fresh], histories, []);

        Assert.Equal(["parked.sql [parked]", "fresh.sql [fresh]"], plan.Ignored.Select(m => m.Id));
        Assert.Empty(plan.Resolved);
        Assert.Empty(plan.ToApply);
        Assert.Empty(plan.ToBaseline);
        Assert.Empty(plan.ToRepair);
    }

    [Fact]
    public void Prepare_TreatsNeverAsAbsent_ForDependsOnUnlessAlreadyApplied()
    {
        var missing = New("parked.sql", "parked", run: Migration.RunMode.Never);
        var dependent = New("Fks/fk.sql", "fk", dependsOn: ["parked.sql"]);

        var (failed, error) = DeploymentPlanner.Prepare([dependent, missing], new Dictionary<string, MigrationHistory>(), []);

        Assert.Null(failed);
        Assert.NotNull(error);
        Assert.Contains("parked.sql", error!.Message);

        var applied = History("parked.sql", "parked", hash: "old");
        var (plan, ok) = DeploymentPlanner.Prepare(
            [dependent, missing],
            new Dictionary<string, MigrationHistory> { [applied.MigrationId] = applied },
            []);

        Assert.Null(ok);
        Assert.NotNull(plan);
        Assert.Equal(["Fks/fk.sql [fk]"], plan!.ToApply.Select(p => p.Migration.Id));
        Assert.Equal(["parked.sql [parked]"], plan.Ignored.Select(m => m.Id));
    }

    private static Migration New(string file, string title, string hash = "h", string[]? contextFilter = null, Migration.RunMode run = Migration.RunMode.Once, string[]? dependsOn = null) => new()
    {
        FileName = file,
        Title = title,
        SqlStatements = ["select 1;"],
        Hash = hash,
        ContextFilter = contextFilter ?? [],
        DependsOn = dependsOn ?? [],
        Run = run
    };

    private static MigrationHistory History(string file, string title, string? hash) => new()
    {
        FileName = file,
        Title = title,
        Hash = hash
    };
}
