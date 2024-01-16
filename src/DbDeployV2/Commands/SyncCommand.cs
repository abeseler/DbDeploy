namespace DbDeploy.Commands;

internal sealed class SyncCommand : ICommand
{
    public string Name => "sync";

    public async Task<Result<Success, Error>> ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
        return new Error("Command not implemented");
    }
}
