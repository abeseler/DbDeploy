namespace DbDeploy;

internal sealed class DeployApp(FileMigrationExtractor migrationExtractor, PathNormalizer pathNormalizer, CommandHandler commandHandler, IOptions<DeploymentOptions> options, ILogger<DeployApp> logger)
{
    private readonly ILogger<DeployApp> _logger = logger;
    private readonly IOptions<DeploymentOptions> _options = options;
    private readonly CommandHandler _commandHandler = commandHandler;
    private readonly FileMigrationExtractor _migrationExtractor = migrationExtractor;
    private readonly PathNormalizer _pathNormalizer = pathNormalizer;
    private readonly FileInfo? _startingFile = pathNormalizer.GetFile(options.Value.MigrationFile);

    public async Task<Result<Success, Error>> RunAsync()
    {
        if (_startingFile is null)
        {
            _logger.LogError("Could not find migration file: {FileName}", _options.Value.MigrationFile);
            return Error.Default;
        }

        var extractResult = await _migrationExtractor.ExtractFromFile(_startingFile);
        var migrations = extractResult.Match(
            onSuccess: migrations => migrations,
            onFailure: exception => []);

        _logger.LogInformation("Found {MigrationCount} migration(s)", migrations.Count);

        if (migrations.Count == 0)
            return Success.Default;

        var commandResult = await _commandHandler.ExecuteAsync(migrations);
        var errorMessage = commandResult.Match(
            onSuccess: _ => string.Empty,
            onFailure: exception => exception.Message);

        _logger.LogInformation("Command result: {CommandResult}", commandResult.IsSuccess ? "Success" : errorMessage);

        return commandResult.IsSuccess ? Success.Default : Error.Default;
    }
}
