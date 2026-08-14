namespace Ratchet.Commands;

internal sealed class CommandResolver(IServiceProvider services)
{
    public ICommand Resolve(string name) => name switch
    {
        CommandNames.Update => services.GetRequiredService<UpdateCommand>(),
        CommandNames.Status => services.GetRequiredService<StatusCommand>(),
        CommandNames.Baseline => services.GetRequiredService<BaselineCommand>(),
        CommandNames.Repair => services.GetRequiredService<RepairCommand>(),
        CommandNames.DryRun => services.GetRequiredService<DryRunCommand>(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown command")
    };
}
