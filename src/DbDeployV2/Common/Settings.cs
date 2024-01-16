namespace DbDeploy.Common;

internal sealed class Settings
{
    public const string SectionName = "Deploy";
    public string? Command { get; set; }
    public string? RootFile { get; set; }
    public string? ConnectionString { get; set; }
}
