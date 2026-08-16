namespace Ratchet.Parsing;

internal sealed class MigrationLoader(IOptions<Settings> settings, ILogger<MigrationLoader> logger)
{
    private readonly string _workingDirectory = Path.GetFullPath(
        string.IsNullOrWhiteSpace(settings.Value.WorkingDirectory) ? Settings.DefaultWorkingDirectory : settings.Value.WorkingDirectory,
        Directory.GetCurrentDirectory());
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public Result<IReadOnlyList<Migration>> Load(CancellationToken stoppingToken)
    {
        var startingFileName = string.IsNullOrWhiteSpace(settings.Value.StartingFile)
            ? Settings.DefaultStartingFile
            : settings.Value.StartingFile;
        var startingFile = new FileInfo(Path.GetFullPath(startingFileName, _workingDirectory));

        logger.LogDebug("Working directory: {WorkingDirectory}", _workingDirectory);
        logger.LogDebug("Starting file: {StartingFile}", startingFile.FullName);

        if (startingFile.Exists is false)
            return Errors.StartingFileDoesNotExist(startingFileName);

        var migrationIncludes = new List<MigrationIncludes>();
        var migrations = new List<Migration>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (startingFile.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var reader = startingFile.OpenRead();
                var parsedIncludes = JsonSerializer.Deserialize<MigrationIncludes[]>(reader, _options);
                if (parsedIncludes is { })
                    migrationIncludes.AddRange(parsedIncludes);
            }
            catch (JsonException ex)
            {
                return Errors.StartingFileParseFailed(ex.Message);
            }
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
            return Errors.StartingFileExtensionNotSupported(startingFile.Extension);
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
                    if (IsSqlFile(file))
                        LoadSqlFile(migrations, seen, file, include, ref errorCount, stoppingToken);
                    else
                        logger.LogDebug("Skipping non-SQL file: {Include}", path);
                    continue;
                }

                if (Directory.Exists(fullPath))
                {
                    logger.LogDebug("Extracting migrations from directory: {Include}", path);
                    foreach (var file in new DirectoryInfo(fullPath).EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        if (IsSqlFile(file) is false)
                        {
                            logger.LogDebug("Skipping non-SQL file in directory {Include}: {File}", path, file.Name);
                            continue;
                        }

                        LoadSqlFile(migrations, seen, file, include, ref errorCount, stoppingToken);
                    }
                    continue;
                }

                if (include.ErrorIfMissingOrEmpty)
                {
                    logger.LogError("{Error}: {Include}", Errors.PathDoesNotExist.Message, path);
                    errorCount++;
                }
            }
        }

        if (errorCount > 0)
            return Errors.MigrationsParsingError(errorCount);

        logger.LogDebug("Parsed {MigrationCount} migration(s)", migrations.Count);
        return migrations;
    }

    private void LoadSqlFile(
        List<Migration> migrations,
        HashSet<string> seen,
        FileInfo file,
        MigrationIncludes include,
        ref int errorCount,
        CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();
        var filePath = GetRelativeFilePath(file);
        Result<List<Migration>> result;
        try
        {
            using var reader = file.OpenText();
            result = SqlFileParser.Parse(reader, filePath, ParseOptions.FromInclude(include), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Error}: {File}", Errors.FileParsingError.Message, filePath);
            errorCount++;
            return;
        }

        var parsed = result.Match(
            onSuccess: parsedMigrations => parsedMigrations,
            onFailure: error =>
            {
                logger.LogError("{Error}: {File}\n{Message}", Errors.FileParsingError.Message, filePath, error.Message);
                return [];
            });

        if (result.Failed)
            errorCount++;

        foreach (var migration in parsed)
        {
            if (seen.Add(migration.Id))
                migrations.Add(migration);
            else
                logger.LogDebug("Skipping already-seen {MigrationId}", migration.Id);
        }
    }

    private string GetRelativeFilePath(FileInfo file)
    {
        var relativePath = Normalize(file.FullName.Replace(_workingDirectory, string.Empty));
        return relativePath.StartsWith('/') ? relativePath[1..] : relativePath;
    }

    private static bool IsSqlFile(FileInfo file) =>
        file.Extension.Equals(".sql", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? path)
    {
        return path?.Replace('\\', '/') ?? string.Empty;
    }
}
