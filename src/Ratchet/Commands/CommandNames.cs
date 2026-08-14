using System.Diagnostics.CodeAnalysis;

namespace Ratchet.Commands;

internal static class CommandNames
{
    public const string Update = "update";
    public const string Status = "status";
    public const string Baseline = "baseline";
    public const string Repair = "repair";
    public const string DryRun = "dryrun";
    public const string Validate = "validate";

    public static readonly string[] All = [Update, Status, Validate, DryRun, Baseline, Repair];

    public static bool RequiresDatabase(string name) => name != Validate;

    public static bool TryNormalize(string? name, [NotNullWhen(true)] out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            normalized = null;
            return false;
        }

        foreach (var command in All)
        {
            if (command.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                normalized = command;
                return true;
            }
        }

        normalized = null;
        return false;
    }
}
