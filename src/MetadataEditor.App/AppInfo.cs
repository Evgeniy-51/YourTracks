using System.Reflection;

namespace MetadataEditor.App;

internal static class AppInfo
{
    public const string ProductName = "YourTracks";

    public static string Version { get; } = ResolveVersion();

    public static string WindowTitle => $"{ProductName}\u2009{Version}";

    private static string ResolveVersion()
    {
        var informational = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "v0.1";
        }

        var label = informational.Split('+')[0];
        return label.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? label : $"v{label}";
    }
}
