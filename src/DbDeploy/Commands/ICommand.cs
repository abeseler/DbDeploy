namespace DbDeploy.Commands;

internal interface ICommand
{
    string Name { get; }
    Task<Result<Success, Error>> ExecuteAsync(CancellationToken stoppingToken);
}
