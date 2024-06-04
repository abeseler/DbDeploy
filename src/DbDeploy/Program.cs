using Dapper;
using DbDeploy.FileHandling;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger());

builder.Configuration.AddCommandLine(args, Arguments.Mapping);

builder.Services.AddHostedService<Worker>();

builder.Services.AddOptions<Settings>().BindConfiguration(Settings.SectionName);
builder.Services.AddSingleton<DbConnector>();
builder.Services.AddSingleton<Repository>();
builder.Services.AddSingleton<FileMigrationExtractor>();
builder.Services.AddSingleton<ICommand, StatusCommand>();
builder.Services.AddSingleton<ICommand, SyncCommand>();
builder.Services.AddSingleton<ICommand, UpdateCommand>();

DefaultTypeMap.MatchNamesWithUnderscores = true;

await builder.Build().RunAsync();
