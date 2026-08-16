namespace Ratchet.Commands;

internal sealed class RepairCommand(MigrationLoader loader, MigrationJournal journal, IOptions<Settings> settings, ILogger<RepairCommand> logger) : ICommand
{
    public string Name => CommandNames.Repair;

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

            var repaired = new List<string>();
            foreach (var (migration, history) in plan.ToRepair)
            {
                stoppingToken.ThrowIfCancellationRequested();
                if (history is null)
                    continue;

                logger.LogWarning(
                    "Repairing {MigrationId}\n  previous = {PreviousHash}\n  current  = {CurrentHash}",
                    migration.Id, history.Hash, migration.Hash);
                await journal.Repair(migration, history, stoppingToken);
                repaired.Add(migration.Id);
            }

            logger.LogInformation("{Report}", PlanReport.Repair(repaired, plan));
            return null;
        }
        finally
        {
            await journal.ReleaseLock(stoppingToken);
        }
    }
}
