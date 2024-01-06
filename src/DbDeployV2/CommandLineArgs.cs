namespace DbDeployV2;

internal static class CommandLineArgs
{
    public static readonly Dictionary<string, string> Mapping = new()
    {
        { "--logLevel", $"Serilog:MinimumLevel:Default" }
    };
}
