namespace DbDeploy.Commands;

internal sealed class StatusCommand(ILogger<StatusCommand> logger) : ICommand
{
    public string Name => "status";

    public async Task<Result<Success, Error>> ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Executing {Command} command", Name);
        await Task.CompletedTask;
        return Errors.CommandNotImplemented;
    }
}
