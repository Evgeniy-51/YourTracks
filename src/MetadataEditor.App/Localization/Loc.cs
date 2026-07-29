using System.Resources;

namespace MetadataEditor.App.Localization;

public static class Loc
{
    private static readonly ResourceManager Manager = new(
        "MetadataEditor.App.Resources.Strings",
        typeof(Loc).Assembly);

    public static string T(string key) =>
        Manager.GetString(key, System.Globalization.CultureInfo.CurrentUICulture) ?? key;

    public static string F(string key, params object[] args) =>
        string.Format(T(key), args);
}
