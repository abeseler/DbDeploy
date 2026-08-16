namespace Ratchet.Commands;

internal interface ICommand
{
    string Name { get; }
    Task<Error?> ExecuteAsync(CancellationToken stoppingToken);
}
