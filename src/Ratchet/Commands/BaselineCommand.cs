using Ratchet.FileHandling;

namespace Ratchet.Commands;

internal sealed class BaselineCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<BaselineCommand> logger) : ICommand
{
    public string Name => CommandNames.Baseline;

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

            var baselined = new List<string>(plan.ToBaseline.Count);
            foreach (var (migration, history) in plan.ToBaseline)
            {
                stoppingToken.ThrowIfCancellationRequested();
                logger.LogInformation("Baselining {MigrationId}", migration.Id);
                await repo.BaselineMigrationHistory(migration, history, stoppingToken);
                baselined.Add(migration.Id);
            }

            logger.LogInformation("{Report}", PlanReport.Baseline(baselined, plan));
            return Success.Default;
        }
        finally
        {
            await repo.ReleaseLock(stoppingToken);
        }
    }
}
