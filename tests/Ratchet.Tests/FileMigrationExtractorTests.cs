using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ratchet.Common;
using Ratchet.FileHandling;
using Xunit;

namespace Ratchet.Tests;

public sealed class FileMigrationExtractorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ratchet-extract-{Guid.NewGuid():N}");

    public FileMigrationExtractorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Extract_TreatsDottedDirectoryNameAsDirectory()
    {
        var dottedDir = Path.Combine(_root, "v2.0");
        Directory.CreateDirectory(dottedDir);
        WriteSql(Path.Combine(_root, "customers.sql"), "customers:create");
        WriteSql(Path.Combine(dottedDir, "orders.sql"), "orders:create");
        File.WriteAllText(Path.Combine(_root, "start.json"), """
            [{ "include": ["customers.sql", "v2.0"] }]
            """);

        var (migrations, error) = Extract("start.json");

        Assert.Null(error);
        Assert.NotNull(migrations);
        Assert.Equal(["customers.sql [customers:create]", "v2.0/orders.sql [orders:create]"], migrations!.Values.Select(m => m.Id).ToList());
    }

    [Fact]
    public void Extract_TreatsExtensionlessFileAsFile_NotMissingDirectory()
    {
        WriteSql(Path.Combine(_root, "bootstrap"), "bootstrap:init");
        File.WriteAllText(Path.Combine(_root, "start.json"), """
            [{ "include": ["bootstrap"] }]
            """);

        var (migrations, error) = Extract("start.json");

        Assert.Null(error);
        Assert.NotNull(migrations);
        Assert.Empty(migrations!.Values);
    }

    [Fact]
    public void Extract_ReturnsPathDoesNotExist_WhenIncludeIsMissing()
    {
        File.WriteAllText(Path.Combine(_root, "start.json"), """
            [{ "include": ["missing.sql"], "errorIfMissingOrEmpty": true }]
            """);

        var (migrations, error) = Extract("start.json");

        Assert.Null(migrations);
        Assert.NotNull(error);
        Assert.Contains("error", error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extract_UsesDefaultStartingFileWhenNotSpecified()
    {
        WriteSql(Path.Combine(_root, "ok.sql"), "ok:create");
        File.WriteAllText(Path.Combine(_root, Settings.DefaultStartingFile), """
            [{ "include": ["ok.sql"] }]
            """);

        var (migrations, error) = Extract(startingFile: null);

        Assert.Null(error);
        Assert.NotNull(migrations);
        Assert.Equal(["ok.sql [ok:create]"], migrations!.Values.Select(m => m.Id).ToList());
    }

    [Fact]
    public void Extract_SkipsMissingInclude_WhenErrorIfMissingOrEmptyIsFalse()
    {
        WriteSql(Path.Combine(_root, "ok.sql"), "ok:create");
        File.WriteAllText(Path.Combine(_root, "start.json"), """
            [
              { "include": ["ok.sql"] },
              { "include": ["nope"], "errorIfMissingOrEmpty": false }
            ]
            """);

        var (migrations, error) = Extract("start.json");

        Assert.Null(error);
        Assert.NotNull(migrations);
        Assert.Equal(["ok.sql [ok:create]"], migrations!.Values.Select(m => m.Id).ToList());
    }

    private Result<Ratchet.Models.MigrationCollection> Extract(string? startingFile)
    {
        var settings = new Settings { WorkingDirectory = _root };
        if (startingFile is not null)
            settings.StartingFile = startingFile;

        var extractor = new FileMigrationExtractor(
            Options.Create(settings),
            NullLogger<FileMigrationExtractor>.Instance);

        return extractor.ExtractFromStartingFile([], CancellationToken.None);
    }

    private static void WriteSql(string path, string title) =>
        File.WriteAllText(path,
            "/* Migration\n{\n    \"title\": \"" + title + "\"\n}\n*/\nSELECT 1;\n");
}
