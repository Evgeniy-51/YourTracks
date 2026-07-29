using System.Windows;

namespace MetadataEditor.App.Views;

public partial class NewFolderPromptDialog : Window
{
    public NewFolderPromptDialog(string title, string prompt, string createLabel, string cancelLabel)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        CreateButton.Content = createLabel;
        CancelButton.Content = cancelLabel;
        FolderNameText.Focus();
    }

    public string FolderName => FolderNameText.Text.Trim();

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
