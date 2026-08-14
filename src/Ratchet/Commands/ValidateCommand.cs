using Ratchet.FileHandling;

namespace Ratchet.Commands;

internal sealed class ValidateCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<ValidateCommand> logger) : ICommand
{
    public string Name => CommandNames.Validate;

    public async Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var (parsed, extractionError) = extractor.ExtractFromStartingFile(stoppingToken);
        if (extractionError is not null)
            return extractionError;

        var contexts = settings.Value.ParseContexts();
        if (settings.Value.IsDatabaseConfigured is false)
        {
            var (ordered, orderError) = MigrationOrderResolver.Resolve(parsed!.Values.ToList(), contexts, []);
            if (orderError is not null)
                return orderError;

            logger.LogInformation("Validation passed (files only). {MigrationCount} migration(s) parsed. No database configured; checksums were not checked.", ordered!.Count);
            return Success.Default;
        }

        var histories = await repo.GetAllMigrationHistories(stoppingToken);
        var (plan, planError) = DeploymentPlanner.Prepare(parsed!.Values.ToList(), histories, contexts);
        if (planError is not null)
            return planError;
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.ToRepair.Count > 0)
        {
            foreach (var (migration, _) in plan.ToRepair)
                logger.LogError("{ErrorMessage}", Exceptions.MigrationHasInvalidChange(migration.Id).Message);
            return Exceptions.ValidationNeedsRepair(plan.ToRepair.Count);
        }

        logger.LogInformation("""
            Validation passed.

                Pending Apply       =  {Applied}
                Pending Baseline    =  {Baseline}
                Needs Repair        =  0
                Filtered out        =  {FilteredOut}

            """, plan.ToApply.Count, plan.ToBaseline.Count, plan.FilteredOut.Count);

        return Success.Default;
    }
}
