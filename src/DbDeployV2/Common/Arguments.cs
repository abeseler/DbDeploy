namespace DbDeploy.Common;

internal static class Arguments
{
    public static readonly Dictionary<string, string> Mapping = new()
    {
        { "--command", "Deploy:Command" },
        { "--rootFile", "Deploy:RootFile" },
        { "--logLevel", "Serilog:MinimumLevel:Default" }
    };
}
