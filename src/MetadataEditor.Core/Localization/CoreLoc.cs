using System.Globalization;
using System.Resources;

namespace MetadataEditor.Core.Localization;

public static class CoreLoc
{
    private static readonly ResourceManager Manager = new(
        "MetadataEditor.Core.Resources.CoreStrings",
        typeof(CoreLoc).Assembly);

    public static string T(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string F(string key, params object[] args) =>
        string.Format(T(key), args);
}
