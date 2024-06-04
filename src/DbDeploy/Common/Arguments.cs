namespace DbDeploy.Common;

internal static class Arguments
{
    public static readonly Dictionary<string, string> Mapping = new()
    {
        { "--command", "Deploy:Command" },
        { "--startingFile", "Deploy:StartingFile" },
        { "--maxLockWait", "Deploy:MaxLockWaitSeconds" },
        { "--contexts", "Deploy:Contexts" },
        { "--dbProvider", "Deploy:DatabaseProvider" },
        { "--connectionStr", "Deploy:ConnectionString" },
        { "--logLevel", "Serilog:MinimumLevel:Default" }
    };
}
