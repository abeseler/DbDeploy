using Ratchet.Models;
using Xunit;

namespace Ratchet.Tests;

public sealed class PlanReportTests
{
    [Fact]
    public void Status_ListsBucketIds_AndDoesNotCountNewFilesAsPendingBaseline()
    {
        var plan = DeploymentPlanner.Build(
            [
                New("seed.sql", "seed", contextFilter: ["prod"]),
                New("keep.sql", "keep", hash: "keep"),
                New("drift.sql", "drift", hash: "new"),
                New("legacy.sql", "legacy", hash: "legacy"),
                New("new.sql", "new", hash: "new")
            ],
            new Dictionary<string, MigrationHistory>
            {
                [Id("keep.sql", "keep")] = History("keep.sql", "keep", "keep"),
                [Id("drift.sql", "drift")] = History("drift.sql", "drift", "old"),
                [Id("legacy.sql", "legacy")] = History("legacy.sql", "legacy", hash: null)
            },
            ["dev"]);

        var report = PlanReport.Status(plan);

        Assert.Contains("Pending apply (1) - would run on update:", report);
        Assert.Contains("    new.sql [new]", report);
        Assert.Contains("Pending baseline (1) - history has no hash; run baseline to accept the current SQL:", report);
        Assert.Contains("    legacy.sql [legacy]", report);
        Assert.DoesNotContain("Pending baseline (2)", report);
        Assert.Contains("Needs repair (1) - SQL changed since apply; run repair to accept the current hash:", report);
        Assert.Contains("    drift.sql [drift]", report);
        Assert.Contains("Up to date (1)", report);
        Assert.Contains("Already in history (3)", report);
        Assert.Contains("Filtered out (1) - skipped by context:", report);
        Assert.Contains("    seed.sql [seed]", report);
    }

    [Fact]
    public void Status_ShowsZeroCounts_WhenPlanIsEmpty()
    {
        var plan = DeploymentPlanner.Build([], new Dictionary<string, MigrationHistory>(), []);

        var report = PlanReport.Status(plan);

        Assert.Contains("Pending apply (0)", report);
        Assert.Contains("Pending baseline (0)", report);
        Assert.Contains("Needs repair (0)", report);
        Assert.Contains("Filtered out (0)", report);
        Assert.DoesNotContain("Pending apply (0) -", report);
    }

    [Fact]
    public void Update_ListsAppliedSkippedAndMarkedIds()
    {
        var plan = DeploymentPlanner.Build(
            [New("keep.sql", "keep", hash: "keep")],
            new Dictionary<string, MigrationHistory>
            {
                [Id("keep.sql", "keep")] = History("keep.sql", "keep", "keep")
            },
            []);

        var report = PlanReport.Update(
            applied: ["a.sql [a]"],
            skipped: ["b.sql [b]"],
            marked: ["c.sql [c]"],
            plan,
            succeeded: true);

        Assert.StartsWith("Update finished.", report);
        Assert.Contains("Applied (1):", report);
        Assert.Contains("    a.sql [a]", report);
        Assert.Contains("Skipped (1) - failed, not recorded, will retry:", report);
        Assert.Contains("    b.sql [b]", report);
        Assert.Contains("Marked (1) - failed, recorded as applied, will not retry:", report);
        Assert.Contains("    c.sql [c]", report);
        Assert.Contains("Already in history (1)", report);
    }

    [Fact]
    public void Update_FailureIncludesRemainingCount()
    {
        var plan = DeploymentPlanner.Build([], new Dictionary<string, MigrationHistory>(), []);

        var report = PlanReport.Update([], [], [], plan, succeeded: false, notApplied: 3);

        Assert.StartsWith("Update failed. 3 migrations not applied.", report);
    }

    [Fact]
    public void Baseline_ListsStampedIds()
    {
        var plan = DeploymentPlanner.Build(
            [New("old.sql", "old", hash: "old")],
            new Dictionary<string, MigrationHistory>(),
            []);

        var report = PlanReport.Baseline(["old.sql [old]"], plan);

        Assert.StartsWith("Baseline finished.", report);
        Assert.Contains("Baselined (1) - recorded as applied without running SQL:", report);
        Assert.Contains("    old.sql [old]", report);
    }

    [Fact]
    public void Repair_ListsRepairedIds()
    {
        var history = History("t.sql", "t", "old");
        var plan = DeploymentPlanner.Build(
            [New("t.sql", "t", hash: "new")],
            new Dictionary<string, MigrationHistory> { [history.MigrationId] = history },
            []);

        var report = PlanReport.Repair(["t.sql [t]"], plan);

        Assert.StartsWith("Repair finished.", report);
        Assert.Contains("Repaired (1) - history hash updated to match the current SQL:", report);
        Assert.Contains("    t.sql [t]", report);
    }

    private static string Id(string file, string title) => Migration.GenerateId(file, title);

    private static Migration New(string file, string title, string hash = "h", string[]? contextFilter = null) => new()
    {
        FileName = file,
        Title = title,
        SqlStatements = ["select 1;"],
        Hash = hash,
        ContextFilter = contextFilter ?? []
    };

    private static MigrationHistory History(string file, string title, string? hash) => new()
    {
        FileName = file,
        Title = title,
        Hash = hash
    };
}
