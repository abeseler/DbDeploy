namespace Ratchet.FileHandling;

internal static class SqlFileParser
{
    public static Result<List<Migration>> Parse(FileInfo file, string relativePath, MigrationIncludes? include, CancellationToken stoppingToken = default)
    {
        if (!file.Exists && (include?.ErrorIfMissingOrEmpty ?? true))
            return Exceptions.FileDoesNotExist;

        var migrations = new List<Migration>();
        var migrationBuilder = new MigrationBuilder(relativePath, include?.ContextFilter ?? [], include?.ContextRequired ?? false);
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
                    if (FinishMigration(migrations, migrationBuilder) is { } duplicate)
                        return duplicate;

                    var remainder = line.Length > 12 ? line[12..] : "";
                    if (TryTakeHeaderClose(remainder, out var sameLineJson))
                    {
                        migrationBuilder.AddHeader(sameLineJson);
                        buildingHeader = false;
                    }
                    else
                    {
                        headerBuilder.Append(remainder);
                        buildingHeader = true;
                    }

                    continue;
                }
                if (buildingHeader)
                {
                    if (TryTakeHeaderClose(line, out var closingJson) is false)
                    {
                        headerBuilder.Append(line);
                        continue;
                    }

                    headerBuilder.Append(closingJson);
                    migrationBuilder.AddHeader(headerBuilder.ToString());
                    headerBuilder.Clear();
                    buildingHeader = false;
                    continue;
                }
                if (line.Length > 0)
                    migrationBuilder.AddToSql(line);
            }

            if (FinishMigration(migrations, migrationBuilder) is { } lastDuplicate)
                return lastDuplicate;
        }
        catch (Exception ex)
        {
            return new Exception(ex.Message);
        }


        return migrations.Count == 0 && (include?.ErrorIfMissingOrEmpty ?? true) ? Exceptions.FileIsEmpty : migrations;
    }

    private static Exception? FinishMigration(List<Migration> migrations, MigrationBuilder builder)
    {
        if (builder.Build() is not { } migration)
            return null;

        if (HasDuplicateTitle(migrations, migration.Title))
            return Exceptions.DuplicateTitle(migration.Title);

        migrations.Add(migration);
        return null;
    }

    private static bool TryTakeHeaderClose(string line, out string beforeClose)
    {
        var trimmed = line.TrimEnd();
        if (trimmed.EndsWith("*/") is false)
        {
            beforeClose = line;
            return false;
        }

        beforeClose = trimmed[..^2];
        return true;
    }

    private static bool HasDuplicateTitle(List<Migration> migrations, string title) =>
        migrations.Any(m => m.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
}
