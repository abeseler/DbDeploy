namespace Ratchet;

internal sealed class App(Repository repository, CommandResolver commands, IOptions<Settings> settings, ILogger<App> logger)
{
    private long _startedTimestamp;
    public async Task RunAsync(CancellationToken stoppingToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Value.Command) || Usage.IsHelpCommand(settings.Value.Command))
        {
            Usage.Write();
            if (string.IsNullOrWhiteSpace(settings.Value.Command))
            {
                logger.LogCritical("No command specified. Set a command from the cli with --command or the environment variable Deploy__Command");
                Environment.ExitCode = 1;
                return;
            }

            Environment.ExitCode = 0;
            return;
        }

        if (CommandNames.TryNormalize(settings.Value.Command, out var commandName) is false)
        {
            Environment.ExitCode = 1;
            logger.LogError("Command '{Command}' is invalid", settings.Value.Command);
            Usage.Write();
            return;
        }

        logger.LogInformation("Starting Ratchet...");
        _startedTimestamp = Stopwatch.GetTimestamp();

        var connectionAttemptsRemaining = settings.Value.ConnectionAttempts;
        var connectionRetryDelay = TimeSpan.FromSeconds(settings.Value.ConnectionRetryDelaySeconds);
        
        while (connectionAttemptsRemaining > 0)
        {
            try
            {
                await repository.EnsureMigrationTablesExist(stoppingToken);
                break;
            }
            catch (Exception ex)
            {
                connectionAttemptsRemaining--;
                if (connectionAttemptsRemaining <= 0)
                {
                    logger.LogCritical("Failed to connect to the database. {ErrorMessage}. No more retries left.", ex.Message);
                    Environment.ExitCode = 1;
                    return;
                }

                logger.LogWarning("Failed to connect to the database. {ErrorMessage}.\nRetrying {RetriesRemaining} more times...", ex.Message, connectionAttemptsRemaining);
                await Task.Delay(connectionRetryDelay, stoppingToken);
            }
        }

        var command = commands.Resolve(commandName);
        var result = await command.ExecuteAsync(stoppingToken);
        var duration = Stopwatch.GetElapsedTime(_startedTimestamp);

        Environment.ExitCode = result.Match(
            onSuccess: _ =>
            {
                logger.LogInformation("Completed successfully in {Duration}", duration);
                return 0;
            },
            onFailure: error =>
            {
                logger.LogError("Command failed: {Error}", error.Message);
                return 1;
            });
    }
}
