using System.Text;

namespace DbDeploy.FileHandling;

internal static class SqlFileParser
{
    public static Result<List<Migration>, Error> Parse(string file, MigrationIncludes? include, CancellationToken stoppingToken = default)
    {
        var fileInfo = new FileInfo(file);
        if (!fileInfo.Exists && (include?.ErrorIfMissingOrEmpty ?? true))
            return Errors.FileDoesNotExist(file);

        var migrations = new List<Migration>();
        var migrationBuilder = new MigrationBuilder(file, include?.ContextFilter ?? [], include?.RequireContext ?? false);
        var headerBuilder = new StringBuilder();
        var buildingHeader = false;

        using var reader = fileInfo.OpenText();
        while (reader.ReadLine() is {} line)
        {
            stoppingToken.ThrowIfCancellationRequested();
            
            if (line.StartsWith("/* Migration", StringComparison.OrdinalIgnoreCase))
            {
                buildingHeader = true;
                if (migrationBuilder.Build() is { } migration)
                    migrations.Add(migration);

                if (line.Length > 12)
                    headerBuilder.Append(line[12..]);

                continue;
            }
            if (buildingHeader)
            {
                var isHeaderEnd = line.EndsWith("*/");
                if (isHeaderEnd is false)
                {
                    headerBuilder.Append(line);
                    continue;
                }

                if (line.Length > 2)
                    headerBuilder.Append(line[..^2]);

                migrationBuilder.AddHeader(headerBuilder.ToString());
                headerBuilder.Clear();
                buildingHeader = false;
                
                continue;
            }
            if (line.Length > 0)
                migrationBuilder.AddToSql(line);
        }

        if (migrationBuilder.Build() is { } lastMigration)
            migrations.Add(lastMigration);

        return migrations.Count == 0 && (include?.ErrorIfMissingOrEmpty ?? true) ? Errors.FileIsEmpty(file) : migrations;
    }
}
