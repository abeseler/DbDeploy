using DbDeploy.Commands;

namespace DbDeploy;

internal sealed class Worker(IHostApplicationLifetime applicationLifetime, IEnumerable<ICommand> commands, IOptions<Settings> settings, ILogger<Worker> logger) : BackgroundService
{
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;
    private readonly ILogger<Worker> _logger = logger;
    private readonly ICommand? command = commands.FirstOrDefault(c => c.Name == settings.Value.Command);
    private readonly Settings _settings = settings.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var start = Stopwatch.GetTimestamp();
        if (command is null)
        {
            Environment.ExitCode = 1;
            _logger.LogError("Command '{Command}' is invalid", _settings.Command);
            _applicationLifetime.StopApplication();
            return;
        }
        _logger.LogInformation("Starting DbDeploy command: {Command}", command.Name);

        var result = await command.ExecuteAsync(stoppingToken);
        var duration = Stopwatch.GetElapsedTime(start);

        Environment.ExitCode = result.Match(
            onSuccess: _ =>
            {
                _logger.LogInformation("Completed successfully in {Duration}ms", duration);
                return 0;
            },
            onFailure: error =>
            {
                _logger.LogError("Failed with error: {Error}", error.Message);
                return 1;
            });

        _applicationLifetime.StopApplication();
    }
}
