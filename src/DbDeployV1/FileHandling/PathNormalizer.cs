namespace DbDeploy.FileHandling;

internal sealed class PathNormalizer(IOptions<DeploymentOptions> options, ILogger<PathNormalizer> logger)
{
    private static readonly string[] _validExtensions = [".sql", ".yaml", ".json"];
    private readonly IOptions<DeploymentOptions> _options = options;
    private readonly ILogger<PathNormalizer> _logger = logger;
    private DirectoryInfo? _workingDirectory;

    public DirectoryInfo WorkingDirectory => _workingDirectory ??= new($"{AppDomain.CurrentDomain.BaseDirectory}/{_options.Value.WorkingDirectory}".Replace('\\', '/'));

    public DirectoryInfo? GetDirectory(string? directoryName)
    {
        var directory = new DirectoryInfo($"{WorkingDirectory.FullName}/{directoryName?.Replace('\\', '/')}");

        if (directory.Exists is false)
        {
            _logger.LogWarning("Directory does not exist: {DirectoryName}", directoryName);
            return null;
        }

        return directory;
    }

    public FileInfo? GetFile(string? fileName)
    {
        var file = new FileInfo($"{WorkingDirectory.FullName}/{fileName?.Replace('\\', '/')}");

        if (file.Exists is false)
        {
            _logger.LogWarning("File does not exist: {FileName}", fileName);
            return null;
        }

        if (_validExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase) is false)
        {
            _logger.LogWarning("Invalid file extension: {FileName}", fileName);
            return null;
        }

        return file;
    }

    public string GetSimpleFileName(FileInfo file)
    {
        return file.FullName.Replace(WorkingDirectory.FullName, "").Replace('\\', '/');
    }
}
