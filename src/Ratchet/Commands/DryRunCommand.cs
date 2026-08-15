using Ratchet.FileHandling;

namespace Ratchet.Commands;

internal sealed class DryRunCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<DryRunCommand> logger) : ICommand
{
    private const string DefaultOutputFile = "ratchet-plan.sql";
    public string Name => CommandNames.DryRun;

    public async Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var (parsed, extractionError) = extractor.ExtractFromStartingFile(stoppingToken);
        if (extractionError is not null)
            return extractionError;

        var histories = await repo.GetAllMigrationHistories(stoppingToken);
        var (plan, planError) = DeploymentPlanner.Prepare(parsed!.Values.ToList(), histories, settings.Value.ParseContexts());
        if (planError is not null)
            return planError;
        ArgumentNullException.ThrowIfNull(plan);

        var outputPath = ResolveOutputPath(settings.Value.OutputFile);
        await WritePlan(outputPath, plan, stoppingToken);

        logger.LogInformation("{Report}", PlanReport.DryRun(plan, outputPath));
        return Success.Default;
    }

    internal static string ResolveOutputPath(string? outputFile)
    {
        var file = string.IsNullOrWhiteSpace(outputFile) ? DefaultOutputFile : outputFile;
        return Path.GetFullPath(file, Directory.GetCurrentDirectory());
    }

    private async Task WritePlan(string path, DeploymentPlan plan, CancellationToken stoppingToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) is false)
            Directory.CreateDirectory(directory);

        await using var writer = new StreamWriter(path, append: false);
        await writer.WriteLineAsync($"-- Ratchet plan generated {DateTimeOffset.UtcNow:u}");
        await writer.WriteLineAsync($"-- Provider: {settings.Value.ResolveDatabaseProvider()}");
        await writer.WriteLineAsync($"-- Pending apply: {plan.ToApply.Count}");
        foreach (var id in PlanReport.Ids(plan.ToApply))
            await writer.WriteLineAsync($"--   {id}");
        await writer.WriteLineAsync($"-- Pending baseline: {plan.PendingBaseline.Count}");
        foreach (var id in PlanReport.Ids(plan.PendingBaseline))
            await writer.WriteLineAsync($"--   {id}");
        await writer.WriteLineAsync($"-- Needs repair: {plan.ToRepair.Count}");
        foreach (var id in PlanReport.Ids(plan.ToRepair))
            await writer.WriteLineAsync($"--   {id}");
        await writer.WriteLineAsync();

        foreach (var (migration, _) in plan.ToApply)
        {
            stoppingToken.ThrowIfCancellationRequested();
            await WriteMigration(writer, migration);
        }

        await WriteReorderFooter(writer, plan.Resolved, plan.Histories);
    }

    // Compares the resolved relative order of already-applied migrations against the order they
    // were applied in the target database (ExecutedSequence). An inversion means a dependency or
    // folder change moved a migration relative to something already applied — harmless on this
    // database, but a signal it could fail on a fresh one unless the ordering is made explicit.
    private static async Task WriteReorderFooter(StreamWriter writer, IReadOnlyList<Migration> resolvedInContext, IReadOnlyDictionary<string, MigrationHistory> histories)
    {
        var applied = resolvedInContext
            .Select((migration, resolvedIndex) => (migration, resolvedIndex, history: histories.GetValueOrDefault(migration.Id)))
            .Where(x => x.history is { ExecutedSequence: not null })
            .OrderBy(x => x.resolvedIndex)
            .ToList();

        var inversions = new List<string>();
        for (var i = 0; i < applied.Count; i++)
        {
            for (var j = i + 1; j < applied.Count; j++)
            {
                if (applied[i].history!.ExecutedSequence > applied[j].history!.ExecutedSequence)
                    inversions.Add($"{applied[j].migration.Id}  now applies AFTER  {applied[i].migration.Id}  (previously before it)");
            }
        }

        await writer.WriteLineAsync("-- ============================================================");
        await writer.WriteLineAsync("-- Reorderings relative to applied history (informational)");
        await writer.WriteLineAsync($"-- Target database applied {applied.Count} of these migrations previously.");
        if (inversions.Count == 0)
        {
            await writer.WriteLineAsync("-- No reorderings relative to applied history.");
            await writer.WriteLineAsync("-- ============================================================");
            return;
        }

        await writer.WriteLineAsync($"-- {inversions.Count} pair(s) now resolve in a different relative order:");
        await writer.WriteLineAsync("-- ------------------------------------------------------------");
        foreach (var inversion in inversions)
            await writer.WriteLineAsync($"--   {inversion}");
        await writer.WriteLineAsync("-- Declare a dependsOn if the new order is required on a fresh database.");
        await writer.WriteLineAsync("-- ============================================================");
    }

    // Transaction boundaries are emitted as comments rather than BEGIN/COMMIT because the executor
    // wraps each migration in an ADO.NET transaction; no such SQL is actually sent to the database.
    private static async Task WriteMigration(StreamWriter writer, Migration migration)
    {
        await writer.WriteLineAsync("-- ============================================================");
        await writer.WriteLineAsync($"-- Migration: {migration.Id}");
        await writer.WriteLineAsync($"-- transaction={(migration.RunInTransaction ? "true" : "false")}  timeout={migration.Timeout}  onError={migration.OnError}");
        if (migration.RunAlways)
            await writer.WriteLineAsync("-- runAlways=true");
        if (migration.RunOnChange)
            await writer.WriteLineAsync("-- runOnChange=true");
        await writer.WriteLineAsync("-- ============================================================");

        if (migration.RunInTransaction)
            await writer.WriteLineAsync("-- transaction begin");

        foreach (var sql in migration.SqlStatements)
        {
            await writer.WriteLineAsync(sql.Trim());
            await writer.WriteLineAsync();
        }

        if (migration.RunInTransaction)
            await writer.WriteLineAsync("-- transaction commit");

        await writer.WriteLineAsync();
    }
}
