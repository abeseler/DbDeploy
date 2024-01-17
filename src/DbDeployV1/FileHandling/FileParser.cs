using DbDeployV1.Commands;
using DbDeployV1.Data;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace DbDeployV1.FileHandling;

internal sealed class FileParser(PathNormalizer normalizer)
{
    private static readonly string[] _validExtensions = [".sql", ".yaml", ".json"];
    private readonly PathNormalizer _normalizer = normalizer;

    public async Task<Result<List<Migration>, Exception>> Parse(FileInfo file)
    {
        var simpleFileName = _normalizer.GetSimpleFileName(file);

        if (file.Exists is false)
            return new ValidationException($"Migration file does not exist: {simpleFileName}");

        if (_validExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase) is false)
            return new ValidationException($"Invalid file extension: {simpleFileName}");

        var text = await File.ReadAllTextAsync(file.FullName);

        if (string.IsNullOrWhiteSpace(text))
            return new ValidationException($"Migration file is empty: {simpleFileName}");

        return file.Extension switch
        {
            var ext when ext.Equals(".sql", StringComparison.OrdinalIgnoreCase) => SqlFileParser.Parse(text, simpleFileName),
            var ext when ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase) => YamlFileParser.Parse(text, simpleFileName),
            var ext when ext.Equals(".json", StringComparison.OrdinalIgnoreCase) => JsonFileParser.Parse(text, simpleFileName),
            _ => throw new UnreachableException($"Unknown file extension read: {file.Extension}")
        };
    }
}
