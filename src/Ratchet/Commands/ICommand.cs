namespace Ratchet.Commands;

internal interface ICommand
{
    string Name { get; }
    Task<Result<Success>> ExecuteAsync(CancellationToken stoppingToken);
}
