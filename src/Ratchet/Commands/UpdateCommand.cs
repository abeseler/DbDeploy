namespace Ratchet.Commands;

internal sealed class UpdateCommand(MigrationLoader loader, MigrationJournal journal, IOptions<Settings> settings, ILogger<UpdateCommand> logger) : ICommand
{
    public string Name => CommandNames.Update;

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

            if (plan.ToRepair.Count > 0)
            {
                foreach (var id in PlanReport.Ids(plan.ToRepair))
                    logger.LogError("{ErrorMessage}", Errors.MigrationHasDrift(id).Message);
                return Errors.UpdateNeedsRepair(plan.ToRepair.Count);
            }

            return await ExecutePlan(plan, stoppingToken);
        }
        finally
        {
            await journal.ReleaseLock(stoppingToken);
        }
    }

    private async Task<Error?> ExecutePlan(DeploymentPlan plan, CancellationToken stoppingToken)
    {
        var applied = new List<string>();
        var skipped = new List<string>();
        var marked = new List<string>();
        var remaining = plan.ToApply.Count;

        foreach (var (migration, history) in plan.ToApply)
        {
            stoppingToken.ThrowIfCancellationRequested();
            logger.LogInformation("Applying {MigrationId}", migration.Id);
            remaining--;

            if (!(await journal.Apply(migration, history, stoppingToken)).TryGet(out var outcome, out _))
            {
                logger.LogInformation("{Report}", PlanReport.Update(applied, skipped, marked, plan, succeeded: false, notApplied: remaining + 1));
                return Errors.DeploymentFailed(remaining + 1);
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
        return null;
    }
}
