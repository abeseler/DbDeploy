using Ratchet.FileHandling;

namespace Ratchet.Commands;

internal sealed class UpdateCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<UpdateCommand> logger) : ICommand
{
    public string Name => CommandNames.Update;

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

            if (plan.ToRepair.Count > 0)
            {
                foreach (var id in PlanReport.Ids(plan.ToRepair))
                    logger.LogError("{ErrorMessage}", Exceptions.MigrationHasInvalidChange(id).Message);
                return Exceptions.UpdateNeedsRepair(plan.ToRepair.Count);
            }

            var result = await ExecutePlan(plan, stoppingToken);
            return result;
        }
        finally
        {
            await repo.ReleaseLock(stoppingToken);
        }
    }

    private async Task<Result<Success>> ExecutePlan(DeploymentPlan plan, CancellationToken stoppingToken)
    {
        var applied = new List<string>();
        var skipped = new List<string>();
        var marked = new List<string>();
        var remaining = plan.ToApply.Count;

        foreach (var (migration, history) in plan.ToApply)
        {
            stoppingToken.ThrowIfCancellationRequested();
            logger.LogInformation("Applying {MigrationId}", migration.Id);
            var result = await repo.ApplyMigration(migration, history, stoppingToken);
            remaining--;

            var outcome = result.Match<ApplyOutcome?>(
                onSuccess: value => value,
                onFailure: _ => null);

            if (outcome is null)
            {
                logger.LogInformation("{Report}", PlanReport.Update(applied, skipped, marked, plan, succeeded: false, notApplied: remaining + 1));
                return Exceptions.DeploymentFailed(remaining + 1);
            }

            switch (outcome)
            {
                case ApplyOutcome.Applied:
                    applied.Add(migration.Id);
                    break;
                case ApplyOutcome.Skipped:
                    skipped.Add(migration.Id);
                    break;
                case ApplyOutcome.Marked:
                    marked.Add(migration.Id);
                    break;
            }
        }

        logger.LogInformation("{Report}", PlanReport.Update(applied, skipped, marked, plan, succeeded: true));
        return Success.Default;
    }
}
