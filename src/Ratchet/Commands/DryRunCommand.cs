namespace Ratchet.Commands;

internal sealed class DryRunCommand(MigrationLoader loader, MigrationJournal journal, IOptions<Settings> settings, ILogger<DryRunCommand> logger) : ICommand
{
    private const string DefaultOutputFile = "ratchet-plan.sql";
    public string Name => CommandNames.DryRun;

    public async Task<Error?> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        if (!loader.Load(stoppingToken).TryGet(out var parsed, out var loadError))
            return loadError;

        var histories = await journal.GetHistories(stoppingToken);
        if (!DeploymentPlanner.Prepare(parsed, histories, settings.Value.ParseContexts()).TryGet(out var plan, out var planError))
            return planError;

        var outputPath = ResolveOutputPath(settings.Value.OutputFile);
        await WritePlan(outputPath, plan, stoppingToken);

        logger.LogInformation("{Report}", PlanReport.DryRun(plan, outputPath));
        return null;
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
        await writer.WriteLineAsync($"-- Ignored: {plan.Ignored.Count}");
        foreach (var id in PlanReport.Ids(plan.Ignored))
            await writer.WriteLineAsync($"--   {id}");
        await writer.WriteLineAsync();

        foreach (var (migration, _) in plan.ToApply)
        {
            stoppingToken.ThrowIfCancellationRequested();
            await WriteMigration(writer, migration);
        }
    }

    // Transaction boundaries are emitted as comments rather than BEGIN/COMMIT because the executor
    // wraps each migration in an ADO.NET transaction; no such SQL is actually sent to the database.
    private static async Task WriteMigration(StreamWriter writer, Migration migration)
    {
        await writer.WriteLineAsync("-- ============================================================");
        await writer.WriteLineAsync($"-- Migration: {migration.Id}");
        await writer.WriteLineAsync($"-- transaction={(migration.RunInTransaction ? "true" : "false")}  timeout={migration.Timeout}  onError={migration.OnError}  run={FormatRun(migration.Run)}");
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

    private static string FormatRun(Migration.RunMode run) => run switch
    {
        Migration.RunMode.OnChange => "onChange",
        Migration.RunMode.Always => "always",
        Migration.RunMode.Never => "never",
        _ => "once"
    };
}
