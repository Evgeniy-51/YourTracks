using System.Windows;
using MetadataEditor.App.Localization;

namespace MetadataEditor.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        LocalizationService.Initialize();

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();

        base.OnStartup(e);
    }
}
