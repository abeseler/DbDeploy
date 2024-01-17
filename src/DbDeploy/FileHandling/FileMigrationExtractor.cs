using System.Text.Json;

namespace DbDeploy.FileHandling;

internal sealed class FileMigrationExtractor(IOptions<Settings> settings, ILogger<FileMigrationExtractor> logger)
{
    private readonly DirectoryInfo _workingDirectory = new(Normalize($"{AppDomain.CurrentDomain.BaseDirectory}/{settings.Value.WorkingDirectory}"));
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public List<Migration> ExtractAll()
    {
        var startingFile = new FileInfo($"{_workingDirectory}/{Normalize(settings.Value.StartingFile)}");
        if (startingFile.Exists is false)
            throw new FileNotFoundException($"Starting file does not exist: {startingFile.FullName}");

        using var reader = startingFile.OpenRead();
        var includes = JsonSerializer.Deserialize<MigrationIncludes[]>(reader, _options);

        var migrations = new List<Migration>();

        return migrations;
    }

    private static string Normalize(string? path)
    {
        return path?.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? string.Empty;
    }
}
