namespace DbDeploy.FileHandling;

internal sealed class FileMigrationExtractor(IOptions<Settings> settings, ILogger<FileMigrationExtractor> logger)
{
    private readonly string _workingDirectory = Path.GetFullPath(Settings.WorkingDirectory, AppDomain.CurrentDomain.BaseDirectory);
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public (MigrationCollection Migrations, int ParsingErrors) ExtractFromStartingFile(CancellationToken stoppingToken)
    {
        var startingFile = new FileInfo(Path.GetFullPath(settings.Value.StartingFile ?? "", _workingDirectory));

        logger.LogDebug("Working directory: {WorkingDirectory}", _workingDirectory);
        logger.LogDebug("Starting file: {StartingFile}", startingFile.FullName);

        if (startingFile.Exists is false)
            throw new FileNotFoundException($"Starting file does not exist: {settings.Value.StartingFile}");

        var migrationIncludes = new List<MigrationIncludes>();
        var migrations = new MigrationCollection();
        if (startingFile.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = startingFile.OpenRead();
            var parsedIncludes = JsonSerializer.Deserialize<MigrationIncludes[]>(reader, _options);

            if (parsedIncludes is { })
                migrationIncludes.AddRange(parsedIncludes);
        }
        else if (startingFile.Extension.Equals(".sql", StringComparison.OrdinalIgnoreCase))
        {
            migrationIncludes.Add(new()
            {
                Include = [settings.Value.StartingFile!]
            });
        }
        else
        {
            throw new NotSupportedException($"Starting file extension is not supported: {startingFile.Extension}");
        }

        var errorCount = 0;
        foreach (var include in migrationIncludes)
        {
            stoppingToken.ThrowIfCancellationRequested();
            foreach (var path in include.Include)
            {
                var fullPath = Path.GetFullPath(path, _workingDirectory);
                if (Path.HasExtension(path))
                {
                    logger.LogDebug("Extracting migrations from file: {Include}", path);
                    var file = new FileInfo(fullPath);
                    if (file.Exists is false && include.ErrorIfMissingOrEmpty)
                    {
                        logger.LogError("{Error}: {Include}", Errors.FileDoesNotExist.Message, path);
                        errorCount++;
                        continue;
                    }

                    if (file.Exists)
                        ExtractMigrationFromSqlFile(migrations, file, include, ref errorCount, stoppingToken);

                    continue;
                }

                logger.LogDebug("Extracting migrations from directory: {Include}", path);
                var directory = new DirectoryInfo(fullPath);
                if (directory.Exists is false && include.ErrorIfMissingOrEmpty)
                {
                    logger.LogError("{Error}: {Include}", Errors.DirectoryDoesNotExist.Message, path);
                    errorCount++;
                    continue;
                }

                if (directory.Exists is false)
                    continue;

                foreach (var file in directory.EnumerateFiles())
                {
                    ExtractMigrationFromSqlFile(migrations, file, include, ref errorCount, stoppingToken);
                }
            }
        }

        return (migrations, errorCount);
    }

    private void ExtractMigrationFromSqlFile(MigrationCollection migrations, FileInfo file, MigrationIncludes include, ref int errorCount, CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();
        var filePath = GetRelativeFilePath(file);
        var result = SqlFileParser.Parse(file, filePath, include, stoppingToken);
        var parsed = result.Match(
            onSuccess: parsedMigrations => parsedMigrations,
            onFailure: error =>
            {
                logger.LogError("{Error}: {File}\n{Message}", Errors.FileParsingError.Message, filePath, error.Message);
                return [];
            });

        if (result.IsFailure)
            errorCount++;

        migrations.AddIntersectionFromRange(parsed);
    }

    private string GetRelativeFilePath(FileInfo file)
    {
        var relativePath = Normalize(file.FullName.Replace(_workingDirectory, string.Empty));
        return relativePath.StartsWith('/') ? relativePath[1..] : relativePath;
    }

    private static string Normalize(string? path)
    {
        return path?.Replace('\\', '/') ?? string.Empty;
    }
}
