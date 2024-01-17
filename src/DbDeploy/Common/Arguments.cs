namespace DbDeploy.Common;

internal static class Arguments
{
    public static readonly Dictionary<string, string> Mapping = new()
    {
        { "--command", "Deploy:Command" },
        { "--startingFile", "Deploy:StartingFile" },
        { "--maxLockWait", "Deploy:MaxLockWaitSeconds" },
        { "--logLevel", "Serilog:MinimumLevel:Default" }
    };
}
