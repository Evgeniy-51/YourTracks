using System.IO;
using System.Windows;
using MetadataEditor.App.Localization;
using MetadataEditor.Core.Models;
using Microsoft.Win32;

namespace MetadataEditor.App.Views;

public partial class SaveOptionsDialog : Window
{
    private readonly string? initialBrowseFolder;

    public SaveOptionsDialog(bool showApplyRename = false, string? initialBrowseFolder = null)
    {
        InitializeComponent();
        this.initialBrowseFolder = initialBrowseFolder;
        Title = Loc.T("SaveDialogTitle");
        HeadingText.Text = Loc.T("SaveDialogHeading");
        ModifyOriginalsRadio.Content = Loc.T("ModifyOriginals");
        CreateBackupsCheck.Content = Loc.T("CreateBackups");
        CopyToFolderRadio.Content = Loc.T("CopyToFolder");
        BrowseButton.Content = Loc.T("Browse");
        NewFolderButton.Content = Loc.T("NewFolder");
        ApplyRenameCheck.Content = Loc.T("ApplyPreviewNames");
        NoteText.Text = Loc.T("SaveDialogNote");
        CancelButton.Content = Loc.T("Cancel");
        SaveButton.Content = Loc.T("Save");

        ApplyRenameCheck.Visibility = showApplyRename
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (!showApplyRename)
        {
            ApplyRenameCheck.IsChecked = false;
        }
    }

    public SaveDialogOptions? Options { get; private set; }

    private void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        SelectCopyToFolderMode();

        var folder = PickFolder(
            Loc.T("Dialog_SelectCopyFolder"),
            GetBrowseStartingFolder());
        if (folder is null)
        {
            return;
        }

        DestinationFolderText.Text = folder;
    }

    private void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        SelectCopyToFolderMode();

        var parentFolder = PickFolder(
            Loc.T("Dialog_SelectNewFolderParent"),
            GetBrowseStartingFolder());
        if (parentFolder is null)
        {
            return;
        }

        var prompt = new NewFolderPromptDialog(
            Loc.T("Dialog_NewFolderTitle"),
            Loc.T("Dialog_NewFolderName"),
            Loc.T("Dialog_CreateFolder"),
            Loc.T("Cancel"))
        {
            Owner = this
        };

        if (prompt.ShowDialog() != true)
        {
            return;
        }

        if (!TryValidateFolderName(prompt.FolderName, out var error))
        {
            MessageBox.Show(
                this,
                error,
                Loc.T("Dialog_NewFolderTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var fullPath = Path.Combine(parentFolder, prompt.FolderName);
        if (Directory.Exists(fullPath))
        {
            MessageBox.Show(
                this,
                Loc.T("Error_NewFolderAlreadyExists"),
                Loc.T("Dialog_NewFolderTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(fullPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                exception.Message,
                Loc.T("Dialog_NewFolderTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        DestinationFolderText.Text = fullPath;
    }

    private void SelectCopyToFolderMode() => CopyToFolderRadio.IsChecked = true;

    private string? GetBrowseStartingFolder()
    {
        if (!string.IsNullOrWhiteSpace(DestinationFolderText.Text))
        {
            var destination = ResolveExistingDirectory(DestinationFolderText.Text);
            if (destination is not null)
            {
                return destination;
            }
        }

        return ResolveExistingDirectory(initialBrowseFolder);
    }

    private static string? ResolveExistingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var current = Path.GetFullPath(path);
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current))
            {
                return current;
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent) ||
                parent.Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        return null;
    }

    private string? PickFolder(string title, string? startingFolder)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(startingFolder) &&
            Directory.Exists(startingFolder))
        {
            dialog.FolderName = startingFolder;
        }

        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private static bool TryValidateFolderName(string folderName, out string error)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            error = Loc.T("Error_NewFolderNameEmpty");
            return false;
        }

        if (folderName is "." or "..")
        {
            error = Loc.T("Error_NewFolderNameInvalid");
            return false;
        }

        if (folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = Loc.T("Error_NewFolderNameInvalid");
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var mode = CopyToFolderRadio.IsChecked == true
            ? SaveMode.CopyToFolder
            : SaveMode.ModifyOriginals;

        if (mode == SaveMode.CopyToFolder &&
            string.IsNullOrWhiteSpace(DestinationFolderText.Text))
        {
            MessageBox.Show(
                this,
                Loc.T("Dialog_SelectCopyFolderWarning"),
                Loc.T("Dialog_SaveTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (mode == SaveMode.CopyToFolder &&
            !Directory.Exists(DestinationFolderText.Text))
        {
            MessageBox.Show(
                this,
                Loc.T("Error_CopyFolderMissing"),
                Loc.T("Dialog_SaveTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var applyRename = ApplyRenameCheck.Visibility == Visibility.Visible &&
                          ApplyRenameCheck.IsChecked == true;

        Options = new SaveDialogOptions(
            mode,
            DestinationFolderText.Text,
            CreateBackupsCheck.IsChecked == true,
            applyRename);
        DialogResult = true;
    }
}

public sealed record SaveDialogOptions(
    SaveMode Mode,
    string DestinationFolder,
    bool CreateBackups,
    bool ApplyRename);
