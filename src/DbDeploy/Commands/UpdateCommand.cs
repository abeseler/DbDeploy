using DbDeploy.Data;

namespace DbDeploy.Commands;

internal sealed class UpdateCommand(Repository repo, ILogger<UpdateCommand> logger) : ICommand
{
    public string Name => "update";

    public async Task<Result<Success, Error>> ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Executing {Command} command", Name);
        await Task.CompletedTask;
        return Errors.CommandNotImplemented;
    }
}
