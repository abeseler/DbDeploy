namespace DbDeploy.FileHandling;

internal static class SqlFileParser
{
    public static Result<List<Migration>> Parse(FileInfo file, string relativePath, MigrationIncludes? include, CancellationToken stoppingToken = default)
    {
        if (!file.Exists && (include?.ErrorIfMissingOrEmpty ?? true))
            return Exceptions.FileDoesNotExist;

        var migrations = new List<Migration>();
        var migrationBuilder = new MigrationBuilder(relativePath, include?.ContextFilter ?? [], include?.RequiresContext ?? false);
        var headerBuilder = new StringBuilder();
        var buildingHeader = false;

        try
        {
            using var reader = file.OpenText();
            while (reader.ReadLine() is { } line)
            {
                stoppingToken.ThrowIfCancellationRequested();

                if (line.StartsWith("/* Migration", StringComparison.OrdinalIgnoreCase))
                {
                    buildingHeader = true;
                    if (migrationBuilder.Build() is { } migration)
                    {
                        if (migrations.Any(m => m.Title == migration.Title))
                            return Exceptions.DuplicateTitle(migration.Title);

                        migrations.Add(migration);
                    }

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
            {
                if (migrations.Any(m => m.Title == lastMigration.Title))
                    return Exceptions.DuplicateTitle(lastMigration.Title);

                migrations.Add(lastMigration);
            }
        }
        catch (Exception ex)
        {
            return new Exception(ex.Message);
        }


        return migrations.Count == 0 && (include?.ErrorIfMissingOrEmpty ?? true) ? Exceptions.FileIsEmpty : migrations;
    }
}
