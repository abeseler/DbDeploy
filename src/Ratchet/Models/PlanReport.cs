namespace Ratchet.Models;

internal static class PlanReport
{
    public static string Status(DeploymentPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Status:");
        sb.AppendLine();
        AppendInspectionBuckets(sb, plan);
        return sb.ToString();
    }

    public static string ValidationPassed(DeploymentPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Validation passed.");
        sb.AppendLine();
        AppendInspectionBuckets(sb, plan);
        return sb.ToString();
    }

    public static string DryRun(DeploymentPlan plan, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Dry run:");
        sb.AppendLine();
        AppendInspectionBuckets(sb, plan);
        sb.AppendLine();
        sb.AppendLine($"  Plan written to     =  {outputPath}");
        return sb.ToString();
    }

    public static string Update(
        IReadOnlyList<string> applied,
        IReadOnlyList<string> skipped,
        IReadOnlyList<string> marked,
        DeploymentPlan plan,
        bool succeeded,
        int notApplied = 0)
    {
        var sb = new StringBuilder();
        if (succeeded)
            sb.AppendLine(applied.Count == 0 && skipped.Count == 0 && marked.Count == 0
                ? "Update finished. Nothing to apply."
                : "Update finished.");
        else
            sb.AppendLine($"Update failed. {notApplied} migration{(notApplied == 1 ? "" : "s")} not applied.");

        sb.AppendLine();
        AppendBucket(sb, "Applied", applied, null);
        AppendBucket(sb, "Skipped", skipped, "failed, not recorded, will retry");
        AppendBucket(sb, "Marked", marked, "failed, recorded as applied, will not retry");
        AppendCount(sb, "Already in history", plan.HistoryCount);
        AppendBucket(sb, "Filtered out", Ids(plan.FilteredOut), "skipped by context");
        return sb.ToString();
    }

    public static string Baseline(IReadOnlyList<string> baselined, DeploymentPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine(baselined.Count == 0 ? "Baseline finished. Nothing to record." : "Baseline finished.");
        sb.AppendLine();
        AppendBucket(sb, "Baselined", baselined, "recorded as applied without running SQL");
        AppendCount(sb, "Already in history", plan.HistoryCount);
        AppendBucket(sb, "Filtered out", Ids(plan.FilteredOut), "skipped by context");
        return sb.ToString();
    }

    public static string Repair(IReadOnlyList<string> repaired, DeploymentPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine(repaired.Count == 0 ? "Repair finished. Nothing to update." : "Repair finished.");
        sb.AppendLine();
        AppendBucket(sb, "Repaired", repaired, "history hash updated to match the current SQL");
        AppendCount(sb, "Already in history", plan.HistoryCount);
        AppendBucket(sb, "Filtered out", Ids(plan.FilteredOut), "skipped by context");
        return sb.ToString();
    }

    public static IReadOnlyList<string> Ids(IEnumerable<PlannedMigration> items) =>
        items.Select(p => p.Migration.Id).ToList();

    public static IReadOnlyList<string> Ids(IEnumerable<Migration> items) =>
        items.Select(m => m.Id).ToList();

    private static void AppendInspectionBuckets(StringBuilder sb, DeploymentPlan plan)
    {
        AppendBucket(sb, "Pending apply", Ids(plan.ToApply), "would run on update");
        AppendBucket(sb, "Pending baseline", Ids(plan.PendingBaseline), "history has no hash; run baseline to accept the current SQL");
        AppendBucket(sb, "Needs repair", Ids(plan.ToRepair), "SQL changed since apply; run repair to accept the current hash");
        AppendCount(sb, "Up to date", plan.UpToDateCount);
        AppendCount(sb, "Already in history", plan.HistoryCount);
        AppendBucket(sb, "Filtered out", Ids(plan.FilteredOut), "skipped by context");
    }

    private static void AppendCount(StringBuilder sb, string name, int count) =>
        sb.AppendLine($"  {name} ({count})");

    private static void AppendBucket(StringBuilder sb, string name, IReadOnlyList<string> ids, string? whenNonEmpty)
    {
        if (ids.Count == 0)
        {
            sb.AppendLine($"  {name} ({ids.Count})");
            return;
        }

        sb.Append($"  {name} ({ids.Count})");
        if (string.IsNullOrEmpty(whenNonEmpty) is false)
            sb.Append(" - ").Append(whenNonEmpty);
        sb.AppendLine(":");
        foreach (var id in ids)
            sb.Append("    ").AppendLine(id);
    }
}
