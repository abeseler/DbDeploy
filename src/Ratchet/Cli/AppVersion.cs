using System.Reflection;

namespace Ratchet.Cli;

internal static class AppVersion
{
    public static string Current { get; } = Read();

    private static string Read()
    {
        var informational = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational) is false)
        {
            var plus = informational.IndexOf('+');
            return plus < 0 ? informational : informational[..plus];
        }

        var version = typeof(AppVersion).Assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
