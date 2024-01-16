namespace DbDeploy.Commands;

internal sealed class UpdateCommand(ILogger<UpdateCommand> logger) : ICommand
{
    public string Name => "update";

    public async Task<Result<Success, Error>> ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Executing {Command} command", Name);
        await Task.CompletedTask;
        return Errors.CommandNotImplemented;
    }
}
