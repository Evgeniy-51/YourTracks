using System.Windows;
using MetadataEditor.App.Localization;

namespace MetadataEditor.App.Views;

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog(int changesCount)
    {
        InitializeComponent();
        Title = Loc.T("UnsavedTitle");
        HeadingText.Text = Loc.T("UnsavedHeading");
        MessageText.Text = changesCount == 1
            ? Loc.T("UnsavedOneFile")
            : Loc.F("UnsavedManyFiles", changesCount);
        CancelButton.Content = Loc.T("Cancel");
        DiscardButton.Content = Loc.T("ExitWithoutSaving");
        SaveButton.Content = Loc.T("Save");
    }

    public UnsavedChangesChoice Choice { get; private set; } =
        UnsavedChangesChoice.Cancel;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedChangesChoice.Save;
        DialogResult = true;
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedChangesChoice.Discard;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedChangesChoice.Cancel;
        DialogResult = false;
    }
}

public enum UnsavedChangesChoice
{
    Cancel,
    Save,
    Discard
}
