namespace Ratchet.Common;

internal static class Usage
{
    public static bool IsHelpRequest(IEnumerable<string> args) =>
        args.Any(a => a.Equals("-h", StringComparison.OrdinalIgnoreCase)
                   || a.Equals("--help", StringComparison.OrdinalIgnoreCase)
                   || a.Equals("-?", StringComparison.OrdinalIgnoreCase)
                   || a.Equals("/?", StringComparison.OrdinalIgnoreCase));

    public static bool IsHelpCommand(string? command) =>
        command is not null && command.Equals("help", StringComparison.OrdinalIgnoreCase);

    public static void Write() => Console.WriteLine(Text);

    private static readonly string CommandList = string.Join("|", CommandNames.All);
    private static readonly string CommandCsv = string.Join(", ", CommandNames.All);

    public static string Text => $"""
        Ratchet — SQL-first database migrations

        Usage:
          ratchet --command <{CommandList}> [options]
          ratchet --help

        Options:
          --command <name>            Command to run: {CommandCsv}
          --migrations <path>         Directory containing the starting file and SQL. Default: Migrations
          --startingFile <file>       Starting file (json include list, or a single .sql file). Default: ratchet.json
          --provider <name>           Database provider: postgres, mssql, sqlite
          --connectionString <cs>     Connection string
          --contexts <list>           Comma-separated contexts
          --maxLockWait <seconds>     Max time to wait for the deployment lock. Default: 120
          --connectionAttempts <n>    Initial connection attempts. Default: 10
          --connectionRetryDelay <s>  Delay between connection attempts. Default: 5
          --outputFile <file>         dryrun plan path. Default: ratchet-plan.sql
          --logLevel <level>          Verbose, Debug, Information, Warning, Error, Fatal
          --help, -h                  Show this help

        Environment variables use the Deploy__ prefix (for example Deploy__Command).
        """;
}
