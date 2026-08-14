using Ratchet.FileHandling;

namespace Ratchet.Commands;

internal sealed class StatusCommand(FileMigrationExtractor extractor, Repository repo, IOptions<Settings> settings, ILogger<StatusCommand> logger) : ICommand
{
    public string Name => "status";

    public async Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        logger.LogInformation("Executing {Command} command", Name);

        var histories = await repo.GetAllMigrationHistories(stoppingToken);
        var (migrations, extractionError) = extractor.ExtractFromStartingFile([.. histories.Values.Select(h => new AppliedMigration(h.FileName, h.Title))], stoppingToken);

        if (extractionError is not null)
            return extractionError;

        var plan = DeploymentPlanner.Build(migrations!.Values, histories, settings.Value.ParseContexts());
        foreach (var migration in plan.InvalidChanges)
            logger.LogWarning("Validation error: {ErrorMessage}", Exceptions.MigrationHasInvalidChange(migration.Id).Message);

        logger.LogInformation("""
            Deployment Results:

                Pending Apply       =  {Applied}
                Previously applied  =  {PreviouslyApplied}
                Pending Sync        =  {Synced}
                Filtered out        =  {FilteredOut}

            """, plan.ToApply.Count, plan.HistoryCount, plan.ToSync.Count, plan.FilteredOut.Count);

        return Success.Default;
    }
}
