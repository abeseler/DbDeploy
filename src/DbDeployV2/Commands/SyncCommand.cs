namespace DbDeploy.Commands;

internal sealed class SyncCommand(ILogger<SyncCommand> logger) : ICommand
{
    public string Name => "sync";

    public async Task<Result<Success, Error>> ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Executing {Command} command", Name);
        await Task.CompletedTask;
        return Errors.CommandNotImplemented;
    }
}
