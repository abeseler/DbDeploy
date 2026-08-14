using Ratchet.FileHandling;

namespace Ratchet.Commands;

internal sealed class RepairCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<RepairCommand> logger) : ICommand
{
    public string Name => CommandNames.Repair;

    public async Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var (parsed, extractionError) = extractor.ExtractFromStartingFile(stoppingToken);
        if (extractionError is not null)
            return extractionError;

        try
        {
            if (await repo.AcquireLock(TimeSpan.FromSeconds(settings.Value.LockWaitMaxSeconds), stoppingToken) is false)
                return Exceptions.FailedToAcquireLock;

            var histories = await repo.GetAllMigrationHistories(stoppingToken);
            var (plan, planError) = DeploymentPlanner.Prepare(parsed!.Values.ToList(), histories, settings.Value.ParseContexts());
            if (planError is not null)
                return planError;
            ArgumentNullException.ThrowIfNull(plan);

            foreach (var (migration, history) in plan.ToRepair)
            {
                stoppingToken.ThrowIfCancellationRequested();
                if (history is null)
                    continue;

                logger.LogWarning("Repairing migration hash: {MigrationId}\n  previous = {PreviousHash}\n  current  = {CurrentHash}", migration.Id, history.Hash, migration.Hash);
                await repo.RepairMigrationHistory(migration, history, stoppingToken);
            }

            logger.LogInformation("""
                Deployment Results:

                  Repaired            =  {Repaired}
                  Previously applied  =  {PreviouslyApplied}
                  Filtered out        =  {FilteredOut}

                """, repo.MigrationsRepaired, plan.HistoryCount, plan.FilteredOut.Count);

            return Success.Default;
        }
        finally
        {
            await repo.ReleaseLock(stoppingToken);
        }
    }
}
