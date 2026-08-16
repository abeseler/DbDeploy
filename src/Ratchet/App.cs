namespace Ratchet;

internal sealed class App(MigrationJournal journal, CommandResolver commands, IOptions<Settings> settings, ILogger<App> logger)
{
    private long _startedTimestamp;
    public async Task RunAsync(CancellationToken stoppingToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Value.Command) || Usage.IsHelpCommand(settings.Value.Command))
        {
            Usage.Write();
            if (string.IsNullOrWhiteSpace(settings.Value.Command))
            {
                logger.LogError("No command specified. Pass a subcommand (ratchet update), --command, or Ratchet__Command");
                Environment.ExitCode = 1;
                return;
            }

            Environment.ExitCode = 0;
            return;
        }

        if (Usage.IsVersionCommand(settings.Value.Command))
        {
            Usage.WriteVersion();
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

        logger.LogInformation("Starting Ratchet {Version} ({Command})", AppVersion.Current, commandName);
        logger.LogDebug("Contexts: {Contexts}", FormatContexts(settings.Value.ParseContexts()));
        _startedTimestamp = Stopwatch.GetTimestamp();

        if (CommandNames.RequiresDatabase(commandName) || settings.Value.IsDatabaseConfigured)
        {
            var connectionAttemptsRemaining = settings.Value.ConnectionAttempts;
            var connectionRetryDelay = TimeSpan.FromSeconds(settings.Value.ConnectionRetryDelaySeconds);

            while (connectionAttemptsRemaining > 0)
            {
                try
                {
                    await journal.EnsureTables(stoppingToken);
                    break;
                }
                catch (Exception ex)
                {
                    connectionAttemptsRemaining--;
                    if (connectionAttemptsRemaining <= 0)
                    {
                        logger.LogError("Failed to connect to the database. {ErrorMessage}. No more retries left.", ex.Message);
                        Environment.ExitCode = 1;
                        return;
                    }

                    logger.LogWarning("Failed to connect to the database. {ErrorMessage}.\nRetrying {RetriesRemaining} more times...", ex.Message, connectionAttemptsRemaining);
                    await Task.Delay(connectionRetryDelay, stoppingToken);
                }
            }
        }

        var command = commands.Resolve(commandName);
        var error = await command.ExecuteAsync(stoppingToken);
        var duration = Stopwatch.GetElapsedTime(_startedTimestamp);

        if (error is not null)
        {
            logger.LogError("Command failed: {Error}", error.Message);
            Environment.ExitCode = 1;
            return;
        }

        logger.LogInformation("Completed {Command} successfully in {Duration}", commandName, duration);
        Environment.ExitCode = 0;
    }

    private static string FormatContexts(string[] contexts) =>
        contexts.Length == 0 ? "(none)" : string.Join(", ", contexts);
}
