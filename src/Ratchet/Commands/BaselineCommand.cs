namespace Ratchet.Commands;

internal sealed class BaselineCommand(MigrationLoader loader, MigrationJournal journal, IOptions<Settings> settings, ILogger<BaselineCommand> logger) : ICommand
{
    public string Name => CommandNames.Baseline;

    public async Task<Error?> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        if (!loader.Load(stoppingToken).TryGet(out var parsed, out var loadError))
            return loadError;

        try
        {
            if (await journal.AcquireLock(TimeSpan.FromSeconds(settings.Value.LockWaitMaxSeconds), stoppingToken) is false)
                return Errors.FailedToAcquireLock;

            var histories = await journal.GetHistories(stoppingToken);
            if (!DeploymentPlanner.Prepare(parsed, histories, settings.Value.ParseContexts()).TryGet(out var plan, out var planError))
                return planError;

            var baselined = new List<string>(plan.ToBaseline.Count);
            foreach (var (migration, history) in plan.ToBaseline)
            {
                stoppingToken.ThrowIfCancellationRequested();
                logger.LogInformation("Baselining {MigrationId}", migration.Id);
                await journal.Baseline(migration, history, stoppingToken);
                baselined.Add(migration.Id);
            }

            logger.LogInformation("{Report}", PlanReport.Baseline(baselined, plan));
            return null;
        }
        finally
        {
            await journal.ReleaseLock(stoppingToken);
        }
    }
}
