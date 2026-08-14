namespace Ratchet.Commands;

internal sealed class CommandResolver(IServiceProvider services)
{
    public ICommand Resolve(string name) => name switch
    {
        CommandNames.Update => services.GetRequiredService<UpdateCommand>(),
        CommandNames.Status => services.GetRequiredService<StatusCommand>(),
        CommandNames.Validate => services.GetRequiredService<ValidateCommand>(),
        CommandNames.DryRun => services.GetRequiredService<DryRunCommand>(),
        CommandNames.Baseline => services.GetRequiredService<BaselineCommand>(),
        CommandNames.Repair => services.GetRequiredService<RepairCommand>(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown command")
    };
}
