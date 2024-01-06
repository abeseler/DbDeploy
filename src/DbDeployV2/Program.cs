using Serilog;

using var host = Host.CreateDefaultBuilder()
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddCommandLine(args, CommandLineArgs.Mapping);
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddSerilog(new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .ReadFrom.Configuration(context.Configuration)
            .CreateLogger());
    })
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<Application>();
    })
    .Build();

using var scope = host.Services.CreateScope();
var app = scope.ServiceProvider.GetRequiredService<Application>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Starting DbDeploy application...");
    var start = Stopwatch.GetTimestamp();
    var result = await app.RunAsync();
    var end = Stopwatch.GetTimestamp();
    Environment.ExitCode = result.Match(
        onSuccess: _ =>
        {
            var elapsed = Stopwatch.GetElapsedTime(start, end);
            logger.LogInformation("Application completed successfully in {Elapsed} seconds", Math.Round(elapsed.TotalSeconds, 2));
            return 0;
        },
        onFailure: error =>
        {
            logger.LogError("Application completed with error: {Error}", error.Message);
            return 1;
        });
}
catch (Exception ex)
{
    logger.LogError(ex, "Application terminated unexpectedly");
    Environment.ExitCode = 1;
}
