using Dapper;
using DbDeploy;
using DbDeploy.FileHandling;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;


var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args, Arguments.Mapping)
    .Build();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
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
            ["service.name"] = config["OTEL_SERVICE_NAME"] ?? "dbdeploy"
        };
    })
    .ReadFrom.Configuration(config)
    .CreateLogger();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddLogging(b =>
{
    b.AddSerilog(dispose: true);
    b.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
    });
});

services.AddOptions<Settings>().BindConfiguration(Settings.SectionName);
services.AddSingleton<App>();

services.AddSingleton<FileMigrationExtractor>();
services.AddSingleton<ICommand, StatusCommand>();
services.AddSingleton<ICommand, SyncCommand>();
services.AddSingleton<ICommand, UpdateCommand>();

services.AddSingleton<Repository>();
services.AddSingleton<IDatabaseProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<Settings>>();
    var connectionString = options.Value.ConnectionString ?? throw new InvalidOperationException("Connection string is not configured.");
    return options.Value.DatabaseProvider switch
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
