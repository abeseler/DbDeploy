namespace Ratchet.Common;

internal static class Arguments
{
    public static readonly Dictionary<string, string> Mapping = new()
    {
        { "--command", "Deploy:Command" },
        { "--migrations", "Deploy:WorkingDirectory" },
        { "--startingFile", "Deploy:StartingFile" },
        { "--maxLockWait", "Deploy:LockWaitMaxSeconds" },
        { "--contexts", "Deploy:Contexts" },
        { "--provider", "Deploy:DatabaseProvider" },
        { "--connectionString", "Deploy:ConnectionString" },
        { "--connectionAttempts", "Deploy:ConnectionAttempts" },
        { "--connectionRetryDelay", "Deploy:ConnectionRetryDelaySeconds" },
        { "--outputFile", "Deploy:OutputFile" },
        { "--logLevel", "Serilog:MinimumLevel:Default" }
    };

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
