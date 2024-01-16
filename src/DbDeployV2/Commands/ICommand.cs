namespace DbDeploy.Commands;

public interface ICommand
{
    string Name { get; }
    Task<Result<Success, Error>> ExecuteAsync(CancellationToken stoppingToken);
}
