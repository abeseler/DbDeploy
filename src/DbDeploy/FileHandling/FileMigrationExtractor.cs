using System.Text.Json;

namespace DbDeploy.FileHandling;

internal sealed class FileMigrationExtractor(IOptions<Settings> settings, ILogger<FileMigrationExtractor> logger)
{
    private readonly DirectoryInfo _workingDirectory = new(Normalize($"{AppDomain.CurrentDomain.BaseDirectory}/{Settings.WorkingDirectory}"));
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public List<Migration> ExtractFromStartingFile()
    {
        var startingFile = new FileInfo($"{_workingDirectory}/{Normalize(settings.Value.StartingFile)}");
        if (startingFile.Exists is false)
            throw new FileNotFoundException($"Starting file does not exist: {startingFile.FullName}");

        var migrations = new List<Migration>();
        if (startingFile.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = startingFile.OpenRead();
            var includes = JsonSerializer.Deserialize<MigrationIncludes[]>(reader, _options);
        }
        else if (startingFile.Extension.Equals(".sql", StringComparison.OrdinalIgnoreCase))
        {
            var result = SqlFileParser.Parse(startingFile.FullName, null);
        }

        

        return migrations;
    }

    private static string Normalize(string? path)
    {
        return path?.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? string.Empty;
    }
}
