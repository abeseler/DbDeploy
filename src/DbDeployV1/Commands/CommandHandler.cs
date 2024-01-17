using DbDeployV1.Data;

namespace DbDeployV1.Commands;

internal sealed class CommandHandler(UpdateCommandHandler updateHandler, SyncCommandHandler syncHandler, StatusCommandHandler statusHandler, IOptions<DeploymentOptions> options, ILogger<CommandHandler> logger)
{
    private readonly ILogger<CommandHandler> _logger = logger;
    private readonly IOptions<DeploymentOptions> _options = options;
    private readonly UpdateCommandHandler _updateHandler = updateHandler;
    private readonly SyncCommandHandler _syncHandler = syncHandler;
    private readonly StatusCommandHandler _statusHandler = statusHandler;

    public Task<Result<Success, Exception>> ExecuteAsync(MigrationCollection migrations)
    {
        var commandType = Enum.TryParse<CommandType>(_options.Value.Command, true, out var commandTypeValue) ? commandTypeValue : CommandType.Invalid;

        _logger.LogInformation("Executing command: {Command}", commandType);

        return commandType switch
        {
            CommandType.Update => _updateHandler.ExecuteAsync(migrations),
            CommandType.Sync => _syncHandler.ExecuteAsync(migrations),
            CommandType.Status => _statusHandler.ExecuteAsync(migrations),
            _ => Task.FromResult((Result<Success, Exception>)new InvalidOperationException($"Command not recognized: {_options.Value.Command}"))
        };
    }

    public enum CommandType
    {
        Invalid,
        Update,
        Sync,
        Status
    }
}
