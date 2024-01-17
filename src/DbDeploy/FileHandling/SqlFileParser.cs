namespace DbDeploy.FileHandling;

internal static class SqlFileParser
{
    public static Result<List<Migration>, Error> Parse(string file, MigrationIncludes include, CancellationToken stoppingToken = default)
    {
        var fileInfo = new FileInfo(file);
        if (!fileInfo.Exists && include.ErrorIfMissingOrEmpty)
            return Errors.FileDoesNotExist(file);

        var builder = new MigrationBuilder(file, include.ContextFilter, include.RequireContext);
        var migrations = new List<Migration>();
        using var reader = fileInfo.OpenText();
        while (reader.ReadLine() is {} line)
        {
            stoppingToken.ThrowIfCancellationRequested();
            Console.WriteLine(line);
        }

        return migrations.Count == 0 && include.ErrorIfMissingOrEmpty ? Errors.FileIsEmpty(file) : migrations;
    }
}
