namespace DbDeployV1;

internal static class CommandArguments
{
    public static readonly Dictionary<string, string> Mappings = new()
    {
        { "--cmd", $"{DeploymentOptions.SectionName}:{nameof(DeploymentOptions.Command)}" },
        { "--conn", $"{DeploymentOptions.SectionName}:{nameof(DeploymentOptions.ConnectionString)}" },
        { "--ctx", $"{DeploymentOptions.SectionName}:{nameof(DeploymentOptions.Contexts)}" },
        { "--file", $"{DeploymentOptions.SectionName}:{nameof(DeploymentOptions.MigrationFile)}" },
        { "--log", $"Serilog:MinimumLevel:Default" }
    };
}
