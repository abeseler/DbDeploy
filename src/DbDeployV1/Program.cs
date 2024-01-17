using DbDeployV1;
using DbDeployV1.Commands;
using DbDeployV1.Data;
using DbDeployV1.FileHandling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

using var host = Host.CreateDefaultBuilder()
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddCommandLine(args, CommandArguments.Mappings);
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddSerilog(new LoggerConfiguration()
            .ReadFrom.Configuration(context.Configuration)
            .CreateLogger());
    })
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<DeployApp>();
        services.AddSingleton<PathNormalizer>();
        services.AddSingleton<FileParser>();
        services.AddSingleton<DbConnector>();
        services.AddSingleton<FileMigrationExtractor>();
        services.AddSingleton<MigrationRepository>();
        services.AddSingleton<CommandHandler>();
        services.AddSingleton<UpdateCommandHandler>();
        services.AddSingleton<SyncCommandHandler>();
        services.AddSingleton<StatusCommandHandler>();
        services.AddOptions<DeploymentOptions>().BindConfiguration(DeploymentOptions.SectionName);
    })
    .Build();

using var scope = host.Services.CreateScope();
var app = scope.ServiceProvider.GetRequiredService<DeployApp>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Starting DbDeploy application...");
    var result = await app.RunAsync();

    Environment.ExitCode = result.Match(
        onSuccess: _ => 0,
        onFailure: _ => 1);
}
catch (Exception ex)
{
    logger.LogError(ex, "Application terminated unexpectedly");
    Environment.ExitCode = 1;
}
