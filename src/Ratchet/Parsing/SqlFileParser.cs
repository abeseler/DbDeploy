namespace Ratchet.Parsing;

internal static class SqlFileParser
{
    public static Result<List<Migration>> Parse(
        string sql,
        string relativePath,
        ParseOptions? options = null,
        CancellationToken stoppingToken = default)
    {
        using var reader = new StringReader(sql);
        return Parse(reader, relativePath, options, stoppingToken);
    }

    public static Result<List<Migration>> Parse(
        TextReader reader,
        string relativePath,
        ParseOptions? options = null,
        CancellationToken stoppingToken = default)
    {
        var migrations = new List<Migration>();
        var migrationBuilder = new MigrationBuilder(relativePath, options?.ContextFilter ?? [], options?.ContextRequired ?? false);
        var headerBuilder = new StringBuilder();
        var buildingHeader = false;

        try
        {
            while (reader.ReadLine() is { } line)
            {
                stoppingToken.ThrowIfCancellationRequested();

                if (SqlFileTokens.TryStartHeader(line, out var remainder))
                {
                    if (FinishMigration(migrations, migrationBuilder) is { } duplicate)
                        return duplicate;

                    if (TryTakeHeaderClose(remainder, out var sameLineJson))
                    {
                        if (migrationBuilder.AddHeader(sameLineJson) is { } headerError)
                            return headerError;
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
                    if (migrationBuilder.AddHeader(headerBuilder.ToString()) is { } headerError)
                        return headerError;
                    headerBuilder.Clear();
                    buildingHeader = false;
                    continue;
                }
                if (SqlFileTokens.IsStatementSeparator(line))
                {
                    migrationBuilder.FinishStatement();
                    continue;
                }
                if (line.Length > 0)
                    migrationBuilder.AddToSql(line);
            }

            if (buildingHeader)
                return Errors.UnclosedMigrationHeader;

            if (FinishMigration(migrations, migrationBuilder) is { } lastDuplicate)
                return lastDuplicate;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Error.From(ex);
        }

        return migrations.Count == 0 && (options?.ErrorIfMissingOrEmpty ?? true) ? Errors.FileIsEmpty : migrations;
    }

    private static Error? FinishMigration(List<Migration> migrations, MigrationBuilder builder)
    {
        if (builder.Build(out var migration) is { } error)
            return error;

        if (migration is null)
            return null;

        if (HasDuplicateTitle(migrations, migration.Title))
            return Errors.DuplicateTitle(migration.Title);

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
