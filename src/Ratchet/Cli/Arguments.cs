namespace Ratchet.Cli;

internal static class Arguments
{
    public static readonly Dictionary<string, string> Mapping = new()
    {
        { "--command", Key("Command") },
        { "--migrations", Key("WorkingDirectory") },
        { "--startingFile", Key("StartingFile") },
        { "--maxLockWait", Key("LockWaitMaxSeconds") },
        { "--contexts", Key("Contexts") },
        { "--provider", Key("DatabaseProvider") },
        { "--connectionString", Key("ConnectionString") },
        { "--connectionAttempts", Key("ConnectionAttempts") },
        { "--connectionRetryDelay", Key("ConnectionRetryDelaySeconds") },
        { "--outputFile", Key("OutputFile") },
        { "--logLevel", "Serilog:MinimumLevel:Default" }
    };

    private static string Key(string name) => $"{Settings.SectionName}:{name}";

    public readonly record struct Parsed(string? PositionalCommand, string[] Remaining, bool CommandSpecifiedTwice);

    public static Parsed Peel(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith('-'))
            return new(null, args, false);

        var remaining = args[1..];
        return new(args[0], remaining, HasCommandFlag(remaining));
    }

    private static bool HasCommandFlag(IEnumerable<string> args) =>
        args.Any(a => a.Equals("--command", StringComparison.OrdinalIgnoreCase)
                   || a.StartsWith("--command=", StringComparison.OrdinalIgnoreCase));
}
