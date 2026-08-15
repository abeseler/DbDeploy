#pragma warning disable ASPIREPROCESSCOMMAND001

internal static class RatchetResourceExtensions
{
    public static IResourceBuilder<ProjectResource> AddRatchetForPostgres(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<IResourceWithConnectionString> database,
        IResourceBuilder<ParameterResource>? contexts = null)
    {
        var ratchet = builder.AddProject<Projects.Ratchet>(name)
            .WithEnvironment("Deploy__Command", "update")
            .WithEnvironment("Deploy__StartingFile", "migrations_postgres.json")
            .WithEnvironment("Deploy__DatabaseProvider", "postgres")
            .WithEnvironment("Deploy__ConnectionString", database)
            .WithEnvironment("Deploy__ConnectionAttempts", "3")
            .WithEnvironment("Deploy__ConnectionRetryDelaySeconds", "5")
            .WithEnvironment("Serilog__MinimumLevel__Default", "Debug")
            .WaitFor(database)
            .WithParentRelationship(database)
            .WithExplicitStart();

        if (contexts is not null)
            ratchet.WithEnvironment("Deploy__Contexts", contexts);

        return ratchet.WithRatchetCommands(builder, database, "postgres", "migrations_postgres.json", contexts);
    }

    public static IResourceBuilder<ProjectResource> WithRatchetCommands(
        this IResourceBuilder<ProjectResource> ratchet,
        IDistributedApplicationBuilder builder,
        IResourceBuilder<IResourceWithConnectionString> database,
        string provider,
        string startingFile,
        IResourceBuilder<ParameterResource>? contexts = null)
    {
        var projectDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "Ratchet"));
        var dll = Path.Combine(projectDir, "bin", GetConfiguration(), "net10.0", "Ratchet.dll");
        var planPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "ratchet-plan.sql"));

        Add(ratchet, database, "status", "Status", "TextBulletListSquare", provider, startingFile, projectDir, dll, planPath, contexts, confirm: false, highlight: true);
        Add(ratchet, database, "validate", "Validate", "Checkmark", provider, startingFile, projectDir, dll, planPath, contexts, confirm: false, highlight: false);
        Add(ratchet, database, "dryrun", "Dry run", "Document", provider, startingFile, projectDir, dll, planPath, contexts, confirm: false, highlight: false);
        Add(ratchet, database, "baseline", "Baseline", "Database", provider, startingFile, projectDir, dll, planPath, contexts, confirm: true, highlight: false);
        Add(ratchet, database, "repair", "Repair", "Wrench", provider, startingFile, projectDir, dll, planPath, contexts, confirm: true, highlight: false);

        return ratchet;
    }

    private static void Add(
        IResourceBuilder<ProjectResource> ratchet,
        IResourceBuilder<IResourceWithConnectionString> database,
        string command,
        string displayName,
        string iconName,
        string provider,
        string startingFile,
        string projectDir,
        string dll,
        string planPath,
        IResourceBuilder<ParameterResource>? contexts,
        bool confirm,
        bool highlight)
    {
        ratchet.WithProcessCommand(
            command,
            displayName,
            async context =>
            {
                var connectionString = await database.Resource.GetConnectionStringAsync(context.CancellationToken)
                    ?? throw new InvalidOperationException($"No connection string for '{database.Resource.Name}'.");

                var spec = new ProcessCommandSpec("dotnet")
                {
                    Arguments = [dll],
                    WorkingDirectory = projectDir
                };

                spec.EnvironmentVariables["Deploy__Command"] = command;
                spec.EnvironmentVariables["Deploy__DatabaseProvider"] = provider;
                spec.EnvironmentVariables["Deploy__StartingFile"] = startingFile;
                spec.EnvironmentVariables["Deploy__WorkingDirectory"] = "Migrations";
                spec.EnvironmentVariables["Deploy__ConnectionString"] = connectionString;
                spec.EnvironmentVariables["Deploy__ConnectionAttempts"] = "3";
                spec.EnvironmentVariables["Deploy__ConnectionRetryDelaySeconds"] = "5";
                spec.EnvironmentVariables["Deploy__OutputFile"] = planPath;
                spec.EnvironmentVariables["Serilog__MinimumLevel__Default"] = "Information";

                if (contexts is not null)
                {
                    var value = await contexts.Resource.GetValueAsync(context.CancellationToken);
                    if (string.IsNullOrWhiteSpace(value) is false)
                        spec.EnvironmentVariables["Deploy__Contexts"] = value;
                }

                return spec;
            },
            new ProcessCommandOptions
            {
                IconName = iconName,
                IconVariant = IconVariant.Regular,
                IsHighlighted = highlight,
                Visibility = ResourceCommandVisibility.UI | ResourceCommandVisibility.Api,
                ConfirmationMessage = confirm
                    ? $"Run '{command}'? This writes __migration_history and does not run SQL."
                    : null,
                DisplayImmediately = true,
                MaxOutputLineCount = 80
            });
    }

    private static string GetConfiguration() =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif
}
