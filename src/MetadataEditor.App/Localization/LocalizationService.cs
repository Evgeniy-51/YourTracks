using System.Globalization;
using System.Windows;

namespace MetadataEditor.App.Localization;

public static class LocalizationService
{
    public static bool IsRestarting { get; set; }

    public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Russian;

    public static void Initialize()
    {
        CurrentLanguage = UserSettingsStore.LoadLanguage();
        Apply(CurrentLanguage);
    }

    public static void SetLanguage(AppLanguage language)
    {
        CurrentLanguage = language;
        UserSettingsStore.SaveLanguage(language);
        Apply(language);
    }

    public static void Apply(AppLanguage language)
    {
        var culture = language == AppLanguage.English
            ? new CultureInfo("en")
            : new CultureInfo("ru");

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static void RestartMainWindow()
    {
        if (Application.Current is not App app)
        {
            return;
        }

        IsRestarting = true;
        try
        {
            var window = new MainWindow();
            app.MainWindow = window;
            window.Show();

            foreach (var openWindow in app.Windows.Cast<Window>().ToList())
            {
                if (!ReferenceEquals(openWindow, window))
                {
                    openWindow.Close();
                }
            }
        }
        finally
        {
            IsRestarting = false;
        }
    }
}
