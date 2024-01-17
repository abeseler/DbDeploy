using DbDeployV1.Commands;
using DbDeployV1.Data;

namespace DbDeployV1.FileHandling;

internal sealed class FileMigrationExtractor(PathNormalizer pathNormalizer, FileParser fileParser, ILogger<FileMigrationExtractor> logger)
{
    private readonly ILogger<FileMigrationExtractor> _logger = logger;
    private readonly PathNormalizer _pathNormalizer = pathNormalizer;
    private readonly FileParser _fileParser = fileParser;
    private readonly Queue<FileInfo> _filesToProcess = new();
    private readonly HashSet<string> _filesProcessed = [];
    private readonly MigrationCollection _migrationCollection = [];
    private int _errorCount;

    public async Task<Result<MigrationCollection, Exception>> ExtractFromFile(FileInfo startingFile)
    {
        _filesToProcess.Enqueue(startingFile);
        _logger.LogTrace("Queued file for processing: {FileName}", startingFile.FullName);

        while (_filesToProcess.Count > 0)
        {
            _logger.LogDebug("File count in processing queue: {FileCount}", _filesToProcess.Count);

            var parsingTasks = GetParsingTasksFromQueue();

            _logger.LogDebug("Parsing {FileCount} file(s) starting", parsingTasks.Count);

            var parsingResults = await Task.WhenAll(parsingTasks);

            _logger.LogDebug("Finished parsing {FileCount} file(s)", parsingTasks.Count);

            foreach (var result in parsingResults)
            {
                HandleParsingResult(result);
            }
            _logger.LogDebug("Finished handling results with {ErrorCount} error(s)", _errorCount);
        }

        return _errorCount == 0
            ? _migrationCollection
            : new Exception($"Encountered {_errorCount} errors while parsing migration file(s).");
    }

    private List<Task<Result<List<Migration>, Exception>>> GetParsingTasksFromQueue()
    {
        var file = _filesToProcess.Dequeue();
        _filesProcessed.Add(file.FullName);
        _logger.LogTrace("Added to processing list: {FileName}", file.FullName);
        var parsingTasks = new List<Task<Result<List<Migration>, Exception>>> { _fileParser.Parse(file) };

        if (file.Extension == ".sql")
        {
            while (_filesToProcess.TryPeek(out var nextFile) && nextFile.Extension == ".sql")
            {
                file = _filesToProcess.Dequeue();
                _filesProcessed.Add(file.FullName);
                _logger.LogTrace("Added to processing list: {FileName}", file.FullName);
                parsingTasks.Add(_fileParser.Parse(file));
            }
        }

        return parsingTasks;
    }

    private void HandleParsingResult(Result<List<Migration>, Exception> result)
    {
        _errorCount += result.IsFailure ? 1 : 0;

        var migrations = result.Match(
            onSuccess: migrationList => migrationList,
            onFailure: exception =>
            {
                _logger.LogError(exception, "Error parsing migration file");
                return [];
            });

        foreach (var migration in migrations)
        {
            _ = migration switch
            {
                { Type: Migration.MigrationType.Sql } => HandleSqlMigration(migration),
                { Type: Migration.MigrationType.Include } => HandleIncludeMigration(migration),
                { Type: Migration.MigrationType.IncludeAll } => HandleIncludeAllMigration(migration),
                _ => false
            };
        }
    }

    private bool HandleSqlMigration(Migration migration)
    {
        var key = migration.GetKey().Replace(_pathNormalizer.WorkingDirectory.FullName, string.Empty);
        _migrationCollection.Add(key, migration);
        _logger.LogTrace("Added migration: {MigrationKey}", key);

        return true;
    }

    private bool HandleIncludeMigration(Migration migration)
    {
        var includeFile = _pathNormalizer.GetFile(migration.Include);
        if (includeFile is null)
        {
            _logger.LogWarning("Could not find include file: {FileName}", migration.Include);
            return false;
        }
        if (_filesProcessed.Contains(includeFile.FullName) || _filesToProcess.Any(f => f.FullName == includeFile.FullName))
            return false;

        _filesToProcess.Enqueue(includeFile);
        _logger.LogTrace("Queued file for processing: {FileName}", includeFile.FullName);

        return true;
    }

    private bool HandleIncludeAllMigration(Migration migration)
    {
        var includeDirectory = _pathNormalizer.GetDirectory(migration.IncludeAll);
        if (includeDirectory is null)
        {
            _logger.LogWarning("Could not find include directory: {DirectoryName}", migration.IncludeAll);
            return false;
        }

        var includeFiles = includeDirectory.GetFiles("*.sql", SearchOption.TopDirectoryOnly);
        foreach (var includeFile in includeFiles)
        {
            if (_filesProcessed.Contains(includeFile.FullName) || _filesToProcess.Any(f => f.FullName == includeFile.FullName))
                continue;

            _filesToProcess.Enqueue(includeFile);
            _logger.LogTrace("Queued file for processing: {FileName}", includeFile.FullName);
        }

        return true;
    }
}
