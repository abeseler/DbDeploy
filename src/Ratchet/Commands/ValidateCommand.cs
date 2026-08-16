namespace Ratchet.Commands;

internal sealed class ValidateCommand(MigrationLoader loader, MigrationJournal journal, IOptions<Settings> settings, ILogger<ValidateCommand> logger) : ICommand
{
    public string Name => CommandNames.Validate;

    public async Task<Error?> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        if (!loader.Load(stoppingToken).TryGet(out var parsed, out var loadError))
            return loadError;

        var contexts = settings.Value.ParseContexts();
        if (settings.Value.IsDatabaseConfigured is false)
        {
            if (!MigrationOrderResolver.Resolve(parsed, contexts, []).TryGet(out var ordered, out var orderError))
                return orderError;

            logger.LogInformation(
                "Validation passed (files only). {MigrationCount} migration(s) parsed and ordered. No database configured; checksums were not checked.",
                ordered.Count);
            return null;
        }

        var histories = await journal.GetHistories(stoppingToken);
        if (!DeploymentPlanner.Prepare(parsed, histories, contexts).TryGet(out var plan, out var planError))
            return planError;

        if (plan.ToRepair.Count > 0)
        {
            foreach (var id in PlanReport.Ids(plan.ToRepair))
                logger.LogError("{ErrorMessage}", Errors.MigrationHasDrift(id).Message);
            return Errors.ValidationNeedsRepair(plan.ToRepair.Count);
        }

        logger.LogInformation("{Report}", PlanReport.ValidationPassed(plan));
        return null;
    }
}
