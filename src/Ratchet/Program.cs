using Dapper;
using Ratchet;
using Ratchet.FileHandling;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;


var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args, Arguments.Mapping)
    .Build();

if (Usage.IsHelpRequest(args) || Usage.IsHelpCommand(config["Deploy:Command"]))
{
    Usage.Write();
    return;
}

var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console();

var otelEndpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
if (string.IsNullOrWhiteSpace(otelEndpoint) is false)
{
    loggerConfiguration.WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = otelEndpoint;
        options.Protocol = OtlpProtocol.HttpProtobuf;
        var headers = config["OTEL_EXPORTER_OTLP_HEADERS"]?.Split(',') ?? [];
        foreach (var header in headers)
        {
            var (key, value) = header.Split('=') switch
            {
                [{ } k, { } v] => (k, v),
                var v => throw new Exception($"Invalid header format {v}")
            };

            options.Headers.Add(key, value);
        }
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = config["OTEL_SERVICE_NAME"] ?? "ratchet"
        };
    });
}

Log.Logger = loggerConfiguration
    .ReadFrom.Configuration(config)
    .CreateLogger();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddLogging(b =>
{
    b.AddSerilog(dispose: true);
});

services.AddOptions<Settings>().BindConfiguration(Settings.SectionName);
services.AddSingleton<App>();

services.AddSingleton<FileMigrationExtractor>();
services.AddSingleton<UpdateCommand>();
services.AddSingleton<StatusCommand>();
services.AddSingleton<ValidateCommand>();
services.AddSingleton<DryRunCommand>();
services.AddSingleton<BaselineCommand>();
services.AddSingleton<RepairCommand>();
services.AddSingleton<CommandResolver>();

services.AddSingleton<Repository>();
services.AddSingleton<IDatabaseProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<Settings>>().Value;
    if (options.IsDatabaseConfigured is false)
        return new UnconfiguredDbProvider();

    var connectionString = options.ConnectionString!;
    return options.DatabaseProvider switch
    {
        "postgres" => new PostgresDbProvider(connectionString),
        "mssql" => new MsSqlDbProvider(connectionString),
        "sqlite" => new SqliteDbProvider(connectionString),
        _ => throw new NotSupportedException("Database provider not supported.")
    };
});

DefaultTypeMap.MatchNamesWithUnderscores = true;

try
{
    using var provider = services.BuildServiceProvider();
    var app = provider.GetRequiredService<App>();
    await app.RunAsync();
}
finally
{
    await Log.CloseAndFlushAsync();
}
