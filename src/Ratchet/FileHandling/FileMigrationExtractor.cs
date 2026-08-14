namespace Ratchet.FileHandling;

internal sealed class FileMigrationExtractor(IOptions<Settings> settings, ILogger<FileMigrationExtractor> logger)
{
    private readonly string _workingDirectory = Path.GetFullPath(
        string.IsNullOrWhiteSpace(settings.Value.WorkingDirectory) ? Settings.DefaultWorkingDirectory : settings.Value.WorkingDirectory,
        Directory.GetCurrentDirectory());
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public Result<MigrationCollection> ExtractFromStartingFile(IReadOnlyCollection<AppliedMigration> applied, CancellationToken stoppingToken)
    {
        var startingFileName = string.IsNullOrWhiteSpace(settings.Value.StartingFile)
            ? Settings.DefaultStartingFile
            : settings.Value.StartingFile;
        var startingFile = new FileInfo(Path.GetFullPath(startingFileName, _workingDirectory));

        logger.LogDebug("Working directory: {WorkingDirectory}", _workingDirectory);
        logger.LogDebug("Starting file: {StartingFile}", startingFile.FullName);

        if (startingFile.Exists is false)
        {
            logger.LogCritical("{Error}: {StartingFile}", Exceptions.FileDoesNotExist.Message, startingFile.FullName);
            throw new FileNotFoundException($"Starting file does not exist: {startingFileName}");
        }

        var migrationIncludes = new List<MigrationIncludes>();
        var migrations = new MigrationCollection();
        if (startingFile.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = startingFile.OpenRead();
            var parsedIncludes = JsonSerializer.Deserialize<MigrationIncludes[]>(reader, _options);

            if (parsedIncludes is { })
                migrationIncludes.AddRange(parsedIncludes);
        }
        else if (startingFile.Extension.EndsWith("sql", StringComparison.OrdinalIgnoreCase))
        {
            migrationIncludes.Add(new()
            {
                Include = [startingFileName]
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
                if (File.Exists(fullPath))
                {
                    logger.LogDebug("Extracting migrations from file: {Include}", path);
                    var file = new FileInfo(fullPath);
                    if (file.Extension.EndsWith("sql", StringComparison.OrdinalIgnoreCase))
                        ExtractMigrationFromSqlFile(migrations, file, include, ref errorCount, stoppingToken);
                    else
                        logger.LogInformation("Skipping non-SQL file: {Include}", path);
                    continue;
                }

                if (Directory.Exists(fullPath))
                {
                    logger.LogDebug("Extracting migrations from directory: {Include}", path);
                    foreach (var file in new DirectoryInfo(fullPath).EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                        ExtractMigrationFromSqlFile(migrations, file, include, ref errorCount, stoppingToken);
                    continue;
                }

                if (include.ErrorIfMissingOrEmpty)
                {
                    logger.LogError("{Error}: {Include}", Exceptions.PathDoesNotExist.Message, path);
                    errorCount++;
                }
            }
        }

        return BuildOrdered(migrations, errorCount, applied);
    }

    private Result<MigrationCollection> BuildOrdered(MigrationCollection migrations, int errorCount, IReadOnlyCollection<AppliedMigration> applied)
    {
        if (errorCount > 0)
            return Exceptions.MigrationsParsingError(errorCount);

        var contexts = settings.Value.Contexts?.Split(',').Select(x => x.Trim()).ToArray() ?? [];
        var (ordered, error) = MigrationOrderResolver.Resolve(migrations.Values.ToList(), contexts, applied);
        if (error is not null)
            return error;

        var result = new MigrationCollection();
        foreach (var migration in ordered!)
            result.Add(migration.Id, migration);

        return result;
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
                logger.LogError("{Error}: {File}\n{Message}", Exceptions.FileParsingError.Message, filePath, error.Message);
                return [];
            });

        if (result.Failed)
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
