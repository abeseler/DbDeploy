using Dapper;
using DbDeploy.FileHandling;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

var builder = Host.CreateApplicationBuilder();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        options.Protocol = OtlpProtocol.HttpProtobuf;
        var headers = builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"]?.Split(',') ?? [];
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
            ["service.name"] = builder.Configuration["OTEL_SERVICE_NAME"] ?? "dbdeploy"
        };
    })
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger());

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

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
