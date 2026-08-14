using Ratchet.FileHandling;

namespace Ratchet.Commands;

internal sealed class StatusCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<StatusCommand> logger) : ICommand
{
    public string Name => CommandNames.Status;

    public async Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var (parsed, extractionError) = extractor.ExtractFromStartingFile(stoppingToken);
        if (extractionError is not null)
            return extractionError;

        var histories = await repo.GetAllMigrationHistories(stoppingToken);
        var (plan, planError) = DeploymentPlanner.Prepare(parsed!.Values.ToList(), histories, settings.Value.ParseContexts());
        if (planError is not null)
            return planError;
        ArgumentNullException.ThrowIfNull(plan);
        foreach (var (migration, _) in plan.ToRepair)
            logger.LogWarning("Needs repair: {ErrorMessage}", Exceptions.MigrationHasInvalidChange(migration.Id).Message);

        logger.LogInformation("""
            Deployment Results:

                Pending Apply       =  {Applied}
                Previously applied  =  {PreviouslyApplied}
                Pending Baseline    =  {Baseline}
                Needs Repair        =  {Repair}
                Filtered out        =  {FilteredOut}

            """, plan.ToApply.Count, plan.HistoryCount, plan.ToBaseline.Count, plan.ToRepair.Count, plan.FilteredOut.Count);

        return Success.Default;
    }
}
