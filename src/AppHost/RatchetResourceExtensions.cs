#pragma warning disable ASPIREPROCESSCOMMAND001

using Microsoft.Extensions.Diagnostics.HealthChecks;

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

        return ratchet;
    }

    public static IResourceBuilder<T> WithRatchetCommands<T>(
        this IResourceBuilder<T> database,
        IDistributedApplicationBuilder builder,
        string provider,
        string startingFile,
        IResourceBuilder<ParameterResource>? contexts = null)
        where T : IResourceWithConnectionString
    {
        var projectDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "Ratchet"));
        var dll = Path.Combine(projectDir, "bin", GetConfiguration(), "net10.0", "Ratchet.dll");
        var planPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "ratchet-plan.sql"));

        Add(database, "update", "Update", "Play", provider, startingFile, projectDir, dll, planPath, contexts, confirm: false);
        Add(database, "status", "Status", "TextBulletListSquare", provider, startingFile, projectDir, dll, planPath, contexts, confirm: false);
        Add(database, "validate", "Validate", "Checkmark", provider, startingFile, projectDir, dll, planPath, contexts, confirm: false);
        Add(database, "dryrun", "Dry run", "Document", provider, startingFile, projectDir, dll, planPath, contexts, confirm: false);
        Add(database, "baseline", "Baseline", "Database", provider, startingFile, projectDir, dll, planPath, contexts, confirm: true);
        Add(database, "repair", "Repair", "Wrench", provider, startingFile, projectDir, dll, planPath, contexts, confirm: true);

        return database;
    }

    private static void Add<T>(
        IResourceBuilder<T> database,
        string command,
        string displayName,
        string iconName,
        string provider,
        string startingFile,
        string projectDir,
        string dll,
        string planPath,
        IResourceBuilder<ParameterResource>? contexts,
        bool confirm)
        where T : IResourceWithConnectionString
    {
        database.WithProcessCommand(
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
                ConfirmationMessage = confirm
                    ? $"Run '{command}' against {database.Resource.Name}? This writes __migration_history and does not run SQL."
                    : null,
                UpdateState = state => state.ResourceSnapshot.HealthStatus is HealthStatus.Unhealthy
                    ? ResourceCommandState.Disabled
                    : ResourceCommandState.Enabled,
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
