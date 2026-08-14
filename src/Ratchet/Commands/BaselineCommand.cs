using Ratchet.FileHandling;

namespace Ratchet.Commands;

internal sealed class BaselineCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<BaselineCommand> logger) : ICommand
{
    public string Name => "baseline";

    public async Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var appliedHistories = await repo.GetAllMigrationHistories(stoppingToken);
        var (migrations, extractionError) = extractor.ExtractFromStartingFile([.. appliedHistories.Values.Select(h => new AppliedMigration(h.FileName, h.Title))], stoppingToken);

        if (extractionError is not null)
            return extractionError;

        try
        {
            if (await repo.AcquireLock(TimeSpan.FromSeconds(settings.Value.LockWaitMaxSeconds), stoppingToken) is false)
                return Exceptions.FailedToAcquireLock;

            var histories = await repo.GetAllMigrationHistories(stoppingToken);
            var plan = DeploymentPlanner.Build(migrations!.Values, histories, settings.Value.ParseContexts());

            foreach (var (migration, history) in plan.ToBaseline)
            {
                stoppingToken.ThrowIfCancellationRequested();
                logger.LogInformation("Baselining migration: {MigrationId}", migration.Id);
                await repo.BaselineMigrationHistory(migration, history, stoppingToken);
            }

            logger.LogInformation("""
                Deployment Results:

                  Baselined           =  {Baselined}
                  Previously applied  =  {PreviouslyApplied}
                  Filtered out        =  {FilteredOut}

                """, repo.MigrationsBaselined, plan.HistoryCount, plan.FilteredOut.Count);

            return Success.Default;
        }
        finally
        {
            await repo.ReleaseLock(stoppingToken);
        }
    }
}
