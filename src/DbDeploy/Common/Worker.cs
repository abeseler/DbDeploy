namespace DbDeploy.Common;

internal sealed class Worker(
    IHostApplicationLifetime applicationLifetime,
    Repository repository,
    IEnumerable<ICommand> commands,
    IOptions<Settings> settings,
    ILogger<Worker> logger) : BackgroundService
{
    private long _startedTimestamp;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (commands.FirstOrDefault(c => c.Name.Equals(settings.Value.Command, StringComparison.OrdinalIgnoreCase)) is not { } command)
        {
            Environment.ExitCode = 1;
            logger.LogError("Command '{Command}' is invalid", settings.Value.Command);
            applicationLifetime.StopApplication();
            return;
        }
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

        applicationLifetime.StopApplication();
    }

    public override async Task StartAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting DbDeploy...");
        _startedTimestamp = Stopwatch.GetTimestamp();

        if (string.IsNullOrWhiteSpace(settings.Value.Command))
        {
            logger.LogCritical("No command specified. Set a command from the cli with --command or the environment variable Deploy__Command");
            Environment.ExitCode = 1;
            applicationLifetime.StopApplication();
            return;
        }

        var connectionAttemptsRemaining = 10;

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
                logger.LogWarning("Failed to connect to the database. {ErrorMessage}.\nRetrying {RetriesRemaining} more times...", ex.Message, connectionAttemptsRemaining);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        await base.StartAsync(stoppingToken);
    }
}
