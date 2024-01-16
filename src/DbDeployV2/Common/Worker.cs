using DbDeploy.Commands;

namespace DbDeploy.Common;

internal sealed class Worker(
    IHostApplicationLifetime applicationLifetime,
    IEnumerable<ICommand> commands,
    IOptions<Settings> settings,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var start = Stopwatch.GetTimestamp();
        if (commands.FirstOrDefault(c => c.Name.Equals(settings.Value.Command, StringComparison.OrdinalIgnoreCase)) is not {} command)
        {
            Environment.ExitCode = 1;
            logger.LogError("Command '{Command}' is invalid", settings.Value.Command);
            applicationLifetime.StopApplication();
            return;
        }
        logger.LogInformation("Starting DbDeploy command: {Command}", command.Name);

        var result = await command.ExecuteAsync(stoppingToken);
        var duration = Stopwatch.GetElapsedTime(start);

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
}
