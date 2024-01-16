
namespace DbDeploy.Commands;

internal sealed class StatusCommand : ICommand
{
    public string Name => "status";

    public async Task<Result<Success, Error>> ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
        return new Error("Command not implemented");
    }
}
