namespace DbDeploy.Commands;

internal sealed class UpdateCommand : ICommand
{
    public string Name => "update";

    public async Task<Result<Success, Error>> ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.CompletedTask;
        return new Error("Command not implemented");
    }
}
