using System.IO;
using System.Text.Json;

namespace MetadataEditor.App.Localization;

internal static class UserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YourTracks",
        "settings.json");

    public static AppLanguage LoadLanguage() => Load().Language;

    public static WindowGeometrySettings? LoadWindowGeometry() => Load().Window;

    public static void SaveLanguage(AppLanguage language)
    {
        var settings = Load();
        settings.Language = language;
        Save(settings);
    }

    public static void SaveWindowGeometry(WindowGeometrySettings geometry)
    {
        var settings = Load();
        settings.Window = geometry;
        Save(settings);
    }

    private static UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new UserSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    private static void Save(UserSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore persistence errors.
        }
    }

    private sealed class UserSettings
    {
        public AppLanguage Language { get; set; } = AppLanguage.Russian;

        public WindowGeometrySettings? Window { get; set; }
    }
}
