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

        logger.LogInformation("{Report}", PlanReport.Status(plan));
        return Success.Default;
    }
}
