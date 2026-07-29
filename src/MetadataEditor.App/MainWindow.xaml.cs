using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MetadataEditor.App.Localization;
using MetadataEditor.App.Services;
using MetadataEditor.App.ViewModels;
using MetadataEditor.App.Views;
using MetadataEditor.Core.Services;

namespace MetadataEditor.App;

public partial class MainWindow : Window
{
    private Point dragStartPoint;
    private AudioFileViewModel? draggedFile;
    private bool allowClose;
    private bool closePromptOpen;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
        var metadataService = new TagLibMetadataService();
        DataContext = new MainViewModel(
            new FolderScanner(metadataService),
            new BatchEditService(),
            new FilenameTemplateService(),
            new FilenameNumberingService(),
            new FileSaveService(metadataService),
            new PlaylistService(),
            new CoverArtService());
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs eventArgs)
    {
        WindowPlacementHelper.Apply(this, UserSettingsStore.LoadWindowGeometry());
    }

    private void MainWindow_Closed(object? sender, EventArgs eventArgs)
    {
        UserSettingsStore.SaveWindowGeometry(WindowPlacementHelper.Capture(this));
    }

    private void Exit_Click(object sender, RoutedEventArgs eventArgs) => Close();

    private async void SwitchLanguage_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel ||
            viewModel.IsBusy ||
            closePromptOpen)
        {
            return;
        }

        if (await ConfirmUnsavedChangesAsync() != UnsavedChangesAction.Proceed)
        {
            return;
        }

        var next = LocalizationService.CurrentLanguage == AppLanguage.Russian
            ? AppLanguage.English
            : AppLanguage.Russian;
        LocalizationService.SetLanguage(next);
        LocalizationService.RestartMainWindow();
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs eventArgs)
    {
        if (allowClose ||
            LocalizationService.IsRestarting ||
            DataContext is not MainViewModel viewModel ||
            !viewModel.HasUnsavedChanges)
        {
            return;
        }

        eventArgs.Cancel = true;
        if (closePromptOpen || viewModel.IsBusy)
        {
            return;
        }

        if (await ConfirmUnsavedChangesAsync() == UnsavedChangesAction.Proceed)
        {
            allowClose = true;
            Close();
        }
    }

    private async Task<UnsavedChangesAction> ConfirmUnsavedChangesAsync()
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return UnsavedChangesAction.Proceed;
        }

        if (!viewModel.HasUnsavedChanges)
        {
            return UnsavedChangesAction.Proceed;
        }

        closePromptOpen = true;
        try
        {
            var dialog = new UnsavedChangesDialog(viewModel.UnsavedChangesCount)
            {
                Owner = this
            };
            dialog.ShowDialog();

            return dialog.Choice switch
            {
                UnsavedChangesChoice.Cancel => UnsavedChangesAction.Cancelled,
                UnsavedChangesChoice.Discard => UnsavedChangesAction.Proceed,
                UnsavedChangesChoice.Save => await viewModel.TrySaveAsync() &&
                    !viewModel.HasUnsavedChanges
                    ? UnsavedChangesAction.Proceed
                    : UnsavedChangesAction.Cancelled,
                _ => UnsavedChangesAction.Cancelled
            };
        }
        finally
        {
            closePromptOpen = false;
        }
    }

    private enum UnsavedChangesAction
    {
        Proceed,
        Cancelled
    }

    private void FilesGridHost_SizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        if (sender is not Border host)
        {
            return;
        }

        var radius = host.CornerRadius.TopLeft;
        host.Clip = new RectangleGeometry(
            new Rect(0, 0, host.ActualWidth, host.ActualHeight),
            radius,
            radius);
    }

    private void FilesDataGrid_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        dragStartPoint = eventArgs.GetPosition(FilesDataGrid);
        var source = eventArgs.OriginalSource as DependencyObject;
        var row = GetRow(source);
        draggedFile = row?.Item as AudioFileViewModel;

        if (row is not null ||
            FindAncestor<DataGridColumnHeader>(source) is not null ||
            FindAncestor<ScrollBar>(source) is not null)
        {
            return;
        }

        FilesDataGrid.UnselectAll();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SelectedFile = null;
        }
    }

    private void FilesDataGrid_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        var row = GetRow(eventArgs.OriginalSource as DependencyObject);
        if (row?.Item is not AudioFileViewModel file ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedFile = file;
        row.Focus();
    }

    private void FilesDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs eventArgs)
    {
        var row = GetRow(eventArgs.OriginalSource as DependencyObject);
        var menu = FilesDataGrid.ContextMenu;
        if (row is null || menu is null)
        {
            eventArgs.Handled = true;
            return;
        }

        // Open beside the row so the filename column stays visible.
        menu.Placement = PlacementMode.Right;
        menu.PlacementTarget = row;
        menu.HorizontalOffset = 6;
        menu.VerticalOffset = 0;
        menu.PlacementRectangle = Rect.Empty;
    }

    private void FilesDataGrid_PreviewMouseMove(
        object sender,
        MouseEventArgs eventArgs)
    {
        if (eventArgs.LeftButton != MouseButtonState.Pressed ||
            draggedFile is null)
        {
            return;
        }

        var position = eventArgs.GetPosition(FilesDataGrid);
        if (Math.Abs(position.X - dragStartPoint.X) <
                SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - dragStartPoint.Y) <
                SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(
            FilesDataGrid,
            draggedFile,
            DragDropEffects.Move);
    }

    private void FilesDataGrid_Drop(object sender, DragEventArgs eventArgs)
    {
        if (draggedFile is null ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var target = GetRow(eventArgs.OriginalSource as DependencyObject)?.Item
            as AudioFileViewModel;
        target ??= viewModel.Files.LastOrDefault();

        if (target is not null)
        {
            viewModel.MoveFile(draggedFile, target);
        }

        draggedFile = null;
    }

    private DataGridRow? GetRow(DependencyObject? source) =>
        source is null
            ? null
            : ItemsControl.ContainerFromElement(FilesDataGrid, source)
                as DataGridRow;

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
