namespace Ratchet.Commands;

internal sealed class StatusCommand(MigrationLoader loader, MigrationJournal journal, IOptions<Settings> settings, ILogger<StatusCommand> logger) : ICommand
{
    public string Name => CommandNames.Status;

    public async Task<Error?> ExecuteAsync(CancellationToken stoppingToken = default)
    {
        if (!loader.Load(stoppingToken).TryGet(out var parsed, out var loadError))
            return loadError;

        var histories = await journal.GetHistories(stoppingToken);
        if (!DeploymentPlanner.Prepare(parsed, histories, settings.Value.ParseContexts()).TryGet(out var plan, out var planError))
            return planError;

        logger.LogInformation("{Report}", PlanReport.Status(plan));
        return null;
    }
}
