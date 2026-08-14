using Ratchet.FileHandling;

namespace Ratchet.Commands;

internal sealed class UpdateCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<UpdateCommand> logger) : ICommand
{
    public string Name => CommandNames.Update;

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

            if (plan.ToRepair is [{ } invalid, ..])
                return Exceptions.MigrationHasInvalidChange(invalid.Migration.Id);

            var result = await ExecutePlan(plan, stoppingToken);
            if (result.Succeeded)
                logger.LogInformation("""
                    Deployment Results:

                      Applied             =  {Applied}
                      Previously applied  =  {PreviouslyApplied}
                      Skipped             =  {Skipped}
                      Marked              =  {Marked}
                      Filtered out        =  {FilteredOut}

                    """, repo.MigrationsApplied, plan.HistoryCount, repo.MigrationsSkipped, repo.MigrationsMarked, plan.FilteredOut.Count);

            return result;
        }
        finally
        {
            await repo.ReleaseLock(stoppingToken);
        }
    }

    private async Task<Result<Success>> ExecutePlan(DeploymentPlan plan, CancellationToken stoppingToken)
    {
        var migrationsProcessed = 0;
        foreach (var (migration, history) in plan.ToApply)
        {
            stoppingToken.ThrowIfCancellationRequested();
            logger.LogInformation("Applying migration: {MigrationId}", migration.Id);
            var result = await repo.ApplyMigration(migration, history, stoppingToken);
            var continueToNextMigration = result.Match(
                onSuccess: _ => true,
                onFailure: error => false);

            if (continueToNextMigration is false)
                return Exceptions.DeploymentFailed(plan.ToApply.Count - migrationsProcessed);

            migrationsProcessed++;
        }

        return Success.Default;
    }
}
