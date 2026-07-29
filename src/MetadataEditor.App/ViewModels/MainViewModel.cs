using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MetadataEditor.Core.Models;
using MetadataEditor.Core.Services;
using MetadataEditor.Core.Validation;
using MetadataEditor.App.Localization;
using MetadataEditor.App.Services;
using MetadataEditor.App.Views;
using Microsoft.Win32;

namespace MetadataEditor.App.ViewModels;

public partial class MainViewModel(
    IFolderScanner folderScanner,
    BatchEditService batchEditService,
    FilenameTemplateService filenameTemplateService,
    FilenameNumberingService filenameNumberingService,
    FileSaveService fileSaveService,
    PlaylistService playlistService,
    CoverArtService coverArtService) : ObservableObject
{
    private CancellationTokenSource? scanCancellation;
    private bool isApplyingBatch;
    private bool isLoadingFiles;
    private readonly Stopwatch loadStopwatch = new();
    private ObservableCollection<AudioFileViewModel>? filesCollectionHooked;

    private static string AudioFilesFilter => Loc.T("Filter_AudioFiles");
    private const int MinimumLoadingOverlayMilliseconds = 500;

    [ObservableProperty]
    private ObservableCollection<AudioFileViewModel> files = [];

    public ObservableCollection<AudioFileViewModel> RenamePreviewFiles { get; } = [];

    public bool HasFiles => Files.Count > 0;
    public int UnsavedChangesCount => Files.Count(file => file.HasChanges);
    public bool HasUnsavedChanges => UnsavedChangesCount > 0;

    partial void OnFilesChanged(ObservableCollection<AudioFileViewModel> value)
    {
        if (filesCollectionHooked is not null)
        {
            filesCollectionHooked.CollectionChanged -= OnFilesCollectionChanged;
        }

        filesCollectionHooked = value;
        value.CollectionChanged += OnFilesCollectionChanged;
        OnPropertyChanged(nameof(HasFiles));
        RefreshUnsavedState();
        SavePlaylistCommand.NotifyCanExecuteChanged();
    }

    private void OnFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasFiles));
        RefreshUnsavedState();
        SavePlaylistCommand.NotifyCanExecuteChanged();
    }

    public IReadOnlyList<FilenameTemplateOption> FilenameTemplateOptions =>
    [
        new("{artist} - {title}", Loc.T("Template_ArtistTitle")),
        new("{title}", Loc.T("Template_Title")),
        new("{album} - {title}", Loc.T("Template_AlbumTitle")),
        new("{artist} - {album} - {title}", Loc.T("Template_ArtistAlbumTitle")),
        new("{track:00} - {artist} - {title}", Loc.T("Template_TrackArtistTitle")),
        new("{track:00} {artist} - {title}", Loc.T("Template_TrackSpaceArtistTitle")),
        new("{track:00} - {title}", Loc.T("Template_TrackTitle")),
        new("{track:00}. {title}", Loc.T("Template_TrackDotTitle"))
    ];

    public IReadOnlyList<FilenameNumberFormatOption> FilenameNumberFormatOptions { get; } =
    [
        new(FilenameNumberFormat.TwoDigitsSpace, "01 "),
        new(FilenameNumberFormat.TwoDigitsDotSpace, "01. "),
        new(FilenameNumberFormat.TwoDigitsDash, "01-"),
        new(FilenameNumberFormat.ThreeDigitsSpace, "001 ")
    ];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedToStartCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedToEndCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetSelectedFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveFromListCommand))]
    private AudioFileViewModel? selectedFile;

    [ObservableProperty]
    private string folderPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(SavePlaylistCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedToStartCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveSelectedToEndCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetSelectedFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveFromListCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = Loc.T("Status_OpenFolderOrFiles");

    [ObservableProperty]
    private string? lastError;

    [ObservableProperty]
    private bool isFolderSource = true;

    [ObservableProperty]
    private PlaylistFormat selectedPlaylistFormat = PlaylistFormat.M3U8;

    [ObservableProperty]
    private bool playlistUseAbsolutePaths;

    [ObservableProperty]
    private BatchApplyScope batchApplyScope;

    [ObservableProperty]
    private bool applyFilenameRename;

    [ObservableProperty]
    private bool applyArtist;

    [ObservableProperty]
    private bool applyTitle;

    [ObservableProperty]
    private bool applyAlbum;

    [ObservableProperty]
    private bool applyYear;

    [ObservableProperty]
    private bool applyClearTrackNumber;

    [ObservableProperty]
    private bool applySetCover;

    [ObservableProperty]
    private bool applyRemoveCover;

    [ObservableProperty]
    private CoverArt? batchCover;

    [ObservableProperty]
    private string batchCoverFileName = string.Empty;

    public bool HasBatchCover => BatchCover is not null;

    [ObservableProperty]
    private bool applyTrackNumbering;

    [ObservableProperty]
    private FilenameTemplateOption selectedFilenameTemplateOption = new(
        "{artist} - {title}",
        Loc.T("Template_ArtistTitle"));

    public string LanguageSwitchLabel =>
        LocalizationService.CurrentLanguage == AppLanguage.Russian ? "EN" : "РУ";

    public string LanguageSwitchTooltip =>
        LocalizationService.CurrentLanguage == AppLanguage.Russian
            ? Loc.T("LanguageTooltipEnglish")
            : Loc.T("LanguageTooltipRussian");

    [ObservableProperty]
    private FilenameNumberFormatOption selectedFilenameNumberFormatOption = new(
        FilenameNumberFormat.TwoDigitsDotSpace,
        "01. ");

    [ObservableProperty]
    private FilenameNumberingSource filenameNumberingSource;

    [ObservableProperty]
    private string batchArtist = string.Empty;

    [ObservableProperty]
    private string batchTitle = string.Empty;

    [ObservableProperty]
    private string batchAlbum = string.Empty;

    [ObservableProperty]
    private string batchYear = string.Empty;

    [ObservableProperty]
    private bool showRenamePreviewPlaceholder;

    public bool ShowRenamePreviewSection =>
        ApplyFilenameRename || ApplyTrackNumbering;

    partial void OnSelectedFilenameTemplateOptionChanged(FilenameTemplateOption value) =>
        UpdateRenamePreview();

    partial void OnApplyFilenameRenameChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRenamePreviewSection));
        UpdateRenamePreview();
    }

    partial void OnApplyTrackNumberingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRenamePreviewSection));
        UpdateRenamePreview();
    }

    partial void OnFilenameNumberingSourceChanged(FilenameNumberingSource value) =>
        UpdateRenamePreview();

    partial void OnSelectedFilenameNumberFormatOptionChanged(
        FilenameNumberFormatOption value) =>
        UpdateRenamePreview();

    partial void OnBatchApplyScopeChanged(BatchApplyScope value) =>
        UpdateRenamePreview();

    partial void OnApplyArtistChanged(bool value) =>
        SyncBatchFieldOnApplyChanged(
            value,
            () => BatchArtist = GetBatchValueSource()?.Artist ?? string.Empty,
            () => BatchArtist = string.Empty);

    partial void OnApplyTitleChanged(bool value) =>
        SyncBatchFieldOnApplyChanged(
            value,
            () => BatchTitle = GetBatchValueSource()?.Title ?? string.Empty,
            () => BatchTitle = string.Empty);

    partial void OnApplyAlbumChanged(bool value) =>
        SyncBatchFieldOnApplyChanged(
            value,
            () => BatchAlbum = GetBatchValueSource()?.Album ?? string.Empty,
            () => BatchAlbum = string.Empty);

    partial void OnApplyYearChanged(bool value) =>
        SyncBatchFieldOnApplyChanged(
            value,
            () => BatchYear = GetBatchValueSource()?.Year?.ToString() ?? string.Empty,
            () => BatchYear = string.Empty);

    partial void OnApplySetCoverChanged(bool value)
    {
        if (!value)
        {
            BatchCover = null;
            BatchCoverFileName = string.Empty;
        }
    }

    partial void OnBatchCoverChanged(CoverArt? value) =>
        OnPropertyChanged(nameof(HasBatchCover));

    partial void OnSelectedFileChanged(AudioFileViewModel? value) => RefreshAutomationFields();

    private static void SyncBatchFieldOnApplyChanged(bool enabled, Action fill, Action clear)
    {
        if (enabled)
        {
            fill();
        }
        else
        {
            clear();
        }
    }

    private bool CanOpenSource() => !IsBusy;

    private bool CanSave() => !IsBusy && HasUnsavedChanges;

    private bool CanResetAll() => !IsBusy && HasUnsavedChanges;

    private bool CanSavePlaylist() => !IsBusy && Files.Count > 0;

    [RelayCommand(CanExecute = nameof(CanOpenSource))]
    private async Task OpenFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = Loc.T("Dialog_SelectFolder"),
            Multiselect = false
        };
        var loadPrepared = false;
        var previousFolderPath = FolderPath;
        var previousIsFolderSource = IsFolderSource;
        dialog.FolderOk += (_, _) =>
        {
            IsFolderSource = true;
            BeginLoad(dialog.FolderName, Loc.T("Status_SearchingFiles"));
            loadPrepared = true;
        };

        if (dialog.ShowDialog() != true)
        {
            CancelPreparedLoad(loadPrepared);
            return;
        }

        IsFolderSource = true;
        if (!loadPrepared)
        {
            BeginLoad(dialog.FolderName, Loc.T("Status_SearchingFiles"));
        }

        await Dispatcher.Yield(DispatcherPriority.Background);
        await LoadFolderCoreAsync(
            dialog.FolderName,
            previousFolderPath,
            previousIsFolderSource);
    }

    [RelayCommand(CanExecute = nameof(CanOpenSource))]
    private async Task OpenFilesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("Dialog_SelectFiles"),
            Filter = AudioFilesFilter,
            Multiselect = true
        };
        var loadPrepared = false;
        dialog.FileOk += (_, _) =>
        {
            IsFolderSource = false;
            BeginLoad(
                BuildFilesSourceLabel(dialog.FileNames),
                Loc.F("LoadingProgress", 0, dialog.FileNames.Length));
            loadPrepared = true;
        };

        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
        {
            CancelPreparedLoad(loadPrepared);
            return;
        }

        IsFolderSource = false;
        if (!loadPrepared)
        {
            BeginLoad(
                BuildFilesSourceLabel(dialog.FileNames),
                Loc.F("LoadingProgress", 0, dialog.FileNames.Length));
        }

        await Dispatcher.Yield(DispatcherPriority.Background);
        await LoadFilesCoreAsync(dialog.FileNames);
    }

    private void CancelPreparedLoad(bool loadPrepared)
    {
        if (!loadPrepared)
        {
            return;
        }

        scanCancellation?.Cancel();
        loadStopwatch.Stop();
        isLoadingFiles = false;
        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(CanResetAll))]
    private void ResetAll()
    {
        foreach (var file in Files)
        {
            file.ResetCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var file in Files)
        {
            file.IsSelected = true;
        }

        UpdateRenamePreview();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var file in Files)
        {
            file.IsSelected = false;
        }

        UpdateRenamePreview();
    }

    public void MoveFile(
        AudioFileViewModel file,
        AudioFileViewModel target)
    {
        var oldIndex = Files.IndexOf(file);
        var newIndex = Files.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
        {
            return;
        }

        Files.Move(oldIndex, newIndex);
        SelectedFile = file;
        NotifyListOrderCommandsChanged();

        if (ApplyTrackNumbering)
        {
            UpdateRenamePreview();
        }
    }

    private bool CanMoveSelectedUp() =>
        !IsBusy && SelectedFile is not null && Files.IndexOf(SelectedFile) > 0;

    private bool CanMoveSelectedDown()
    {
        if (IsBusy || SelectedFile is null)
        {
            return false;
        }

        var index = Files.IndexOf(SelectedFile);
        return index >= 0 && index < Files.Count - 1;
    }

    private bool CanResetSelectedFile() =>
        !IsBusy && SelectedFile?.HasChanges == true;

    private bool CanRemoveFromList() =>
        !IsBusy && GetRemovalTargets().Count > 0;

    [RelayCommand(CanExecute = nameof(CanMoveSelectedUp))]
    private void MoveSelectedUp() => MoveSelectedToIndex(Files.IndexOf(SelectedFile!) - 1);

    [RelayCommand(CanExecute = nameof(CanMoveSelectedDown))]
    private void MoveSelectedDown() => MoveSelectedToIndex(Files.IndexOf(SelectedFile!) + 1);

    [RelayCommand(CanExecute = nameof(CanMoveSelectedUp))]
    private void MoveSelectedToStart() => MoveSelectedToIndex(0);

    [RelayCommand(CanExecute = nameof(CanMoveSelectedDown))]
    private void MoveSelectedToEnd() => MoveSelectedToIndex(Files.Count - 1);

    [RelayCommand(CanExecute = nameof(CanResetSelectedFile))]
    private void ResetSelectedFile()
    {
        SelectedFile?.ResetCommand.Execute(null);
        ResetSelectedFileCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveFromList))]
    private void RemoveFromList()
    {
        var targets = GetRemovalTargets();
        if (targets.Count == 0)
        {
            return;
        }

        var message = targets.Count == 1
            ? Loc.F("Msg_RemoveOne", targets[0].FileName)
            : Loc.F("Msg_RemoveMany", targets.Count);

        var confirmed = MessageBox.Show(
            message,
            Loc.T("Msg_RemoveTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;

        if (!confirmed)
        {
            return;
        }

        var removedSelected = SelectedFile is not null && targets.Contains(SelectedFile);
        foreach (var file in targets)
        {
            Files.Remove(file);
        }

        if (removedSelected)
        {
            SelectedFile = null;
        }

        NotifyListOrderCommandsChanged();
        UpdateRenamePreview();
        StatusText = Loc.F("Status_FilesCount", Files.Count);
    }

    private void MoveSelectedToIndex(int newIndex)
    {
        if (SelectedFile is null)
        {
            return;
        }

        var oldIndex = Files.IndexOf(SelectedFile);
        if (oldIndex < 0 || newIndex < 0 || newIndex >= Files.Count || oldIndex == newIndex)
        {
            return;
        }

        Files.Move(oldIndex, newIndex);
        NotifyListOrderCommandsChanged();

        if (ApplyTrackNumbering)
        {
            UpdateRenamePreview();
        }
    }

    private List<AudioFileViewModel> GetRemovalTargets()
    {
        if (SelectedFile is null)
        {
            return [];
        }

        var checkedFiles = Files.Where(file => file.IsSelected).ToList();
        if (SelectedFile.IsSelected && checkedFiles.Count > 1)
        {
            return checkedFiles;
        }

        return [SelectedFile];
    }

    private void NotifyListOrderCommandsChanged()
    {
        MoveSelectedUpCommand.NotifyCanExecuteChanged();
        MoveSelectedDownCommand.NotifyCanExecuteChanged();
        MoveSelectedToStartCommand.NotifyCanExecuteChanged();
        MoveSelectedToEndCommand.NotifyCanExecuteChanged();
        ResetSelectedFileCommand.NotifyCanExecuteChanged();
        RemoveFromListCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ApplyAutomation()
    {
        if (!HasAnyAutomationOperation())
        {
            LastError = Loc.T("Error_SelectOperation");
            return;
        }

        if (ApplySetCover && BatchCover is null)
        {
            LastError = Loc.T("Error_BatchCoverNotSelected");
            return;
        }

        var targets = GetBatchTargets();
        if (targets.Count == 0)
        {
            MessageBox.Show(
                Loc.T("Error_NoFilesSelected"),
                Loc.T("AutomationTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            isApplyingBatch = true;
            var plans = BuildAutomationPlans(targets);

            foreach (var plan in plans)
            {
                plan.File.UpdateMetadata(plan.Metadata);
                if (plan.FileName is not null)
                {
                    plan.File.EditedFileName = plan.FileName;
                    ValidateEditedFileName(plan.File);
                }
            }

            LastError = null;
            if (ApplyFilenameRename || ApplyTrackNumbering)
            {
                RefreshRenamePreview();
            }

            StatusText = Loc.F("Status_AutomationApplied", targets.Count);
        }
        catch (Exception exception) when (
            exception is FormatException or OverflowException)
        {
            LastError = exception.Message;
        }
        finally
        {
            isApplyingBatch = false;
            RefreshUnsavedState();
        }
    }

    private bool HasAnyAutomationOperation() =>
        ApplyFilenameRename ||
        ApplyArtist ||
        ApplyTitle ||
        ApplyAlbum ||
        ApplyYear ||
        ApplyClearTrackNumber ||
        ApplySetCover ||
        ApplyRemoveCover ||
        ApplyTrackNumbering;

    private List<AutomationPlan> BuildAutomationPlans(
        IReadOnlyList<AudioFileViewModel> targets)
    {
        var plans = new List<AutomationPlan>(targets.Count);

        foreach (var file in targets)
        {
            var metadata = ApplyAutomationMetadata(file.ToMetadata());
            string? fileName = null;

            if (ApplyFilenameRename)
            {
                var baseName = filenameTemplateService.Render(
                    SelectedFilenameTemplateOption.Template,
                    metadata);
                fileName = baseName + Path.GetExtension(file.SourcePath);
            }

            if (ApplyTrackNumbering)
            {
                fileName ??= file.EditedFileName;
                uint? number = FilenameNumberingSource == FilenameNumberingSource.TrackField
                    ? metadata.TrackNumber
                    : checked((uint)(Files.IndexOf(file) + 1));

                if (number is uint trackNumber)
                {
                    fileName = filenameNumberingService.Apply(
                        fileName,
                        trackNumber,
                        SelectedFilenameNumberFormatOption.Format);
                }
            }

            plans.Add(new AutomationPlan(file, metadata, fileName));
        }

        return plans;
    }

    private AudioMetadata ApplyAutomationMetadata(AudioMetadata metadata)
    {
        if (ApplyArtist)
        {
            metadata = metadata with { Artist = BatchArtist };
        }

        if (ApplyTitle)
        {
            metadata = metadata with { Title = BatchTitle };
        }

        if (ApplyAlbum)
        {
            metadata = metadata with { Album = BatchAlbum };
        }

        if (ApplyYear)
        {
            metadata = batchEditService.Apply(metadata, MetadataField.Year, BatchYear);
        }

        if (ApplyClearTrackNumber)
        {
            metadata = metadata with { TrackNumber = null };
        }

        if (ApplyRemoveCover)
        {
            metadata = metadata with { Cover = null };
        }
        else if (ApplySetCover && BatchCover is not null)
        {
            metadata = metadata with { Cover = BatchCover };
        }

        return metadata;
    }

    private void UpdateRenamePreview()
    {
        if (!ApplyFilenameRename && !ApplyTrackNumbering)
        {
            ClearRenamePreview();
            return;
        }

        RefreshRenamePreview();
    }

    private void ClearRenamePreview()
    {
        RenamePreviewFiles.Clear();
        ShowRenamePreviewPlaceholder = false;

        foreach (var file in Files)
        {
            file.ProposedFileName = file.FileName;
            file.RenameError = null;
        }
    }

    private void RefreshRenamePreview()
    {
        ClearRenamePreview();

        if (!ApplyFilenameRename && !ApplyTrackNumbering)
        {
            return;
        }

        var targets = GetBatchTargets();
        if (targets.Count == 0)
        {
            ShowRenamePreviewPlaceholder = true;
            return;
        }

        foreach (var file in targets)
        {
            RenamePreviewFiles.Add(file);
        }

        try
        {
            foreach (var plan in BuildAutomationPlans(targets))
            {
                plan.File.ProposedFileName = plan.FileName ?? plan.File.FileName;
            }
        }
        catch (FormatException exception)
        {
            LastError = exception.Message;
            return;
        }

        DetectRenameConflicts();
        ReportRenamePreviewSkips(targets);
    }

    private void ReportRenamePreviewSkips(IReadOnlyList<AudioFileViewModel> targets)
    {
        var skipped = targets
            .Where(file => file.RenameError is not null)
            .ToList();
        if (skipped.Count == 0)
        {
            return;
        }

        LastError = skipped.Count == 1
            ? Loc.F(
                "Error_RenameSkippedOne",
                skipped[0].FileName,
                skipped[0].RenameError ?? string.Empty)
            : Loc.F("Error_RenameSkippedMany", skipped.Count);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => TrySaveAsync();

    public async Task<bool> TrySaveAsync()
    {
        if (Files.Count == 0)
        {
            LastError = Loc.T("Error_NoLoadedFiles");
            return false;
        }

        var dialog = new SaveOptionsDialog(
            HasApplicableRenamePreview(),
            GetSaveSourceRoot())
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dialog.ShowDialog() != true || dialog.Options is null)
        {
            return false;
        }

        var options = dialog.Options;
        if (options.ApplyRename)
        {
            RefreshRenamePreview();
        }

        if (Files.Any(file => HasSaveBlockingError(file, options)))
        {
            LastError = Loc.T("Error_FixFileNames");
            return false;
        }

        var candidates = Files
            .Where(file =>
                file.HasMetadataChanges ||
                NeedsRename(file, options))
            .ToList();
        if (candidates.Count == 0)
        {
            LastError = null;
            StatusText = Loc.T("Status_NoChangesToSave");
            RefreshUnsavedState();
            return !HasUnsavedChanges;
        }

        var request = new SaveRequest(
            GetSaveSourceRoot(),
            candidates.Select(file =>
            {
                var applyRename = NeedsRename(file, options);
                var destinationName = applyRename
                    ? FileNameValidator.Normalize(
                        ResolveSaveFileName(file, options),
                        Path.GetExtension(file.SourcePath))
                    : file.FileName;

                return new SaveFileRequest(
                    file.SourcePath,
                    file.ToMetadata(),
                    destinationName,
                    file.HasMetadataChanges,
                    applyRename);
            }).ToList(),
            options.Mode,
            options.DestinationFolder,
            options.ApplyRename,
            options.CreateBackups);

        IsBusy = true;
        LastError = null;
        await Dispatcher.Yield(DispatcherPriority.Background);
        var progress = new Progress<SaveProgress>(value =>
            StatusText = Loc.F(
                "Status_Saving",
                value.Completed,
                value.Total,
                value.FileName));

        try
        {
            var result = await fileSaveService.SaveAsync(
                request,
                progress,
                CancellationToken.None);

            if (options.Mode == SaveMode.ModifyOriginals)
            {
                foreach (var item in result.Items.Where(item => item.Success))
                {
                    var file = Files.First(candidate => string.Equals(
                        candidate.SourcePath,
                        item.SourcePath,
                        StringComparison.OrdinalIgnoreCase));
                    file.AcceptSaved(item.DestinationPath!);
                }

                if (ApplyFilenameRename || ApplyTrackNumbering)
                {
                    RefreshRenamePreview();
                }
            }
            else
            {
                foreach (var item in result.Items.Where(item => item.Success))
                {
                    var file = Files.First(candidate => string.Equals(
                        candidate.SourcePath,
                        item.SourcePath,
                        StringComparison.OrdinalIgnoreCase));
                    file.RevertToLoadedState();
                }

                UpdateRenamePreview();
            }

            var firstError = result.Items.FirstOrDefault(item => !item.Success);
            LastError = firstError?.Error;
            StatusText =
                Loc.F("Status_SavedSummary", result.SuccessfulCount, result.FailedCount);
            RefreshUnsavedState();
            return result.FailedCount == 0 && !HasUnsavedChanges;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or
            UnauthorizedAccessException)
        {
            LastError = exception.Message;
            StatusText = Loc.T("Status_SaveFailed");
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ReplaceCover()
    {
        if (SelectedFile is null)
        {
            return;
        }

        var path = PickCoverImagePath();
        if (path is null)
        {
            return;
        }

        try
        {
            SelectedFile.Cover = coverArtService.LoadFromFile(path);
            LastError = null;
        }
        catch (FormatException exception)
        {
            LastError = exception.Message;
        }
    }

    [RelayCommand]
    private async Task SelectBatchCoverAsync()
    {
        var path = PickCoverImagePath();
        if (path is null)
        {
            return;
        }

        try
        {
            BatchCover = coverArtService.LoadFromFile(path);
            BatchCoverFileName = Path.GetFileName(path);
            LastError = null;
        }
        catch (FormatException exception)
        {
            LastError = exception.Message;
        }

        await Task.CompletedTask;
    }

    private string? PickCoverImagePath()
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("Dialog_SelectCover"),
            Filter = Loc.T("Dialog_CoverFilter")
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    [RelayCommand]
    private void RemoveCover()
    {
        if (SelectedFile is not null)
        {
            SelectedFile.Cover = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSavePlaylist))]
    private async Task SavePlaylistAsync()
    {
        if (Files.Count == 0)
        {
            MessageBox.Show(
                Loc.T("Error_NoLoadedFiles"),
                Loc.T("Dialog_PlaylistTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var format = SelectedPlaylistFormat;
        var extension = PlaylistService.GetExtension(format);
        var dialog = new SaveFileDialog
        {
            Title = Loc.T("Dialog_SavePlaylist"),
            Filter = PlaylistService.GetFilter(format),
            DefaultExt = extension,
            FileName = "playlist" + extension,
            AddExtension = true
        };

        if (!string.IsNullOrWhiteSpace(FolderPath) &&
            Directory.Exists(FolderPath))
        {
            dialog.InitialDirectory = FolderPath;
        }
        else if (Files.Count > 0)
        {
            dialog.InitialDirectory = Path.GetDirectoryName(Files[0].SourcePath);
        }

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var tracks = Files
            .Select(file => new PlaylistTrack(
                file.SourcePath,
                BuildPlaylistTitle(file)))
            .ToList();

        try
        {
            IsBusy = true;
            await playlistService.SaveAsync(
                dialog.FileName,
                tracks,
                format,
                PlaylistUseAbsolutePaths,
                CancellationToken.None);

            LastError = null;
            StatusText = Loc.F("Status_PlaylistSaved", dialog.FileName);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or IOException or
            UnauthorizedAccessException)
        {
            LastError = exception.Message;
            StatusText = Loc.T("Status_PlaylistSaveFailed");
            MessageBox.Show(
                exception.Message,
                Loc.T("Dialog_PlaylistTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildPlaylistTitle(AudioFileViewModel file)
    {
        if (!string.IsNullOrWhiteSpace(file.Artist) &&
            !string.IsNullOrWhiteSpace(file.Title))
        {
            return $"{file.Artist} - {file.Title}";
        }

        if (!string.IsNullOrWhiteSpace(file.Title))
        {
            return file.Title;
        }

        return Path.GetFileNameWithoutExtension(file.FileName);
    }

    private async Task LoadFolderCoreAsync(
        string path,
        string previousFolderPath,
        bool previousIsFolderSource)
    {
        var errorCount = 0;
        var loadedItems = new List<AudioFileItem>();
        var total = 0;
        string? lastError = null;
        var progressThrottle = Stopwatch.StartNew();

        var filesProgress = new InlineProgress<AudioFileItem>(item =>
        {
            loadedItems.Add(item);
            ReportLoadingProgressThrottled(
                loadedItems.Count,
                total,
                progressThrottle);
        });
        var errorProgress = new InlineProgress<string>(error =>
        {
            errorCount++;
            lastError = error;
        });
        var totalProgress = new InlineProgress<int>(value =>
        {
            total = value;
            ReportLoadingProgress(0, total);
        });

        try
        {
            await folderScanner.ScanAsync(
                path,
                filesProgress,
                errorProgress,
                totalProgress,
                scanCancellation!.Token);

            if (total == 0)
            {
                FolderPath = previousFolderPath;
                IsFolderSource = previousIsFolderSource;
                LastError = null;
                StatusText = Files.Count > 0
                    ? Loc.F("Status_FilesLoadedCount", Files.Count)
                    : Loc.T("Status_OpenFolderOrFiles");
                MessageBox.Show(
                    Loc.T("Msg_EmptyFolder"),
                    Loc.T("Msg_EmptyFolderTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            ReplaceLoadedFiles(loadedItems);
            LastError = lastError;
            FinishLoad(
                errorCount,
                Loc.F("Status_FilesLoadedSummary", Files.Count, errorCount));
        }
        catch (OperationCanceledException)
        {
            StatusText = Loc.T("Status_ScanCancelled");
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            StatusText = Loc.T("Status_ReadFolderFailed");
        }
        finally
        {
            await EnsureMinimumLoadingOverlayDurationAsync();
            isLoadingFiles = false;
            IsBusy = false;
        }
    }

    private async Task LoadFilesCoreAsync(IReadOnlyList<string> paths)
    {
        var errorCount = 0;
        var loadedItems = new List<AudioFileItem>();
        string? lastError = null;
        var progressThrottle = Stopwatch.StartNew();

        var filesProgress = new InlineProgress<AudioFileItem>(item =>
        {
            loadedItems.Add(item);
            ReportLoadingProgressThrottled(
                loadedItems.Count,
                paths.Count,
                progressThrottle);
        });
        var errorProgress = new InlineProgress<string>(error =>
        {
            errorCount++;
            lastError = error;
        });

        try
        {
            await folderScanner.LoadFilesAsync(
                paths,
                filesProgress,
                errorProgress,
                scanCancellation!.Token);

            ReplaceLoadedFiles(loadedItems);
            LastError = lastError;
            FinishLoad(
                errorCount,
                Loc.F("Status_FilesLoadedSummary", Files.Count, errorCount));
        }
        catch (OperationCanceledException)
        {
            StatusText = Loc.T("Status_LoadCancelled");
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            StatusText = Loc.T("Status_LoadFailed");
        }
        finally
        {
            await EnsureMinimumLoadingOverlayDurationAsync();
            isLoadingFiles = false;
            IsBusy = false;
        }
    }

    private void BeginLoad(string sourceLabel, string progressLabel)
    {
        scanCancellation?.Cancel();
        scanCancellation?.Dispose();
        scanCancellation = new CancellationTokenSource();

        loadStopwatch.Restart();
        isLoadingFiles = true;
        IsBusy = true;
        FolderPath = sourceLabel;
        LastError = null;
        StatusText = progressLabel;
    }

    private void ReplaceLoadedFiles(IReadOnlyList<AudioFileItem> loadedItems)
    {
        foreach (var file in Files)
        {
            file.PropertyChanged -= OnFilePropertyChanged;
        }

        var viewModels = loadedItems.Select(CreateFileViewModel).ToList();
        Files = new ObservableCollection<AudioFileViewModel>(viewModels);
        SelectedFile = viewModels.FirstOrDefault();
    }

    private async Task EnsureMinimumLoadingOverlayDurationAsync()
    {
        var remaining = MinimumLoadingOverlayMilliseconds -
            (int)loadStopwatch.ElapsedMilliseconds;
        if (remaining > 0)
        {
            await Task.Delay(remaining);
        }

        loadStopwatch.Stop();
    }

    private AudioFileViewModel CreateFileViewModel(AudioFileItem file)
    {
        var viewModel = new AudioFileViewModel(file);
        viewModel.PropertyChanged += OnFilePropertyChanged;
        return viewModel;
    }

    private void ReportLoadingProgressThrottled(
        int loaded,
        int total,
        Stopwatch throttle)
    {
        if (throttle.ElapsedMilliseconds < 100)
        {
            return;
        }

        ReportLoadingProgress(loaded, total);
        throttle.Restart();
    }

    private void ReportLoadingProgress(int loaded, int total)
    {
        Application.Current.Dispatcher.Invoke(
            () => StatusText = total > 0
                ? Loc.F("Status_Loading", loaded, total)
                : Loc.T("Status_SearchingFiles"),
            DispatcherPriority.Background);
    }

    private void FinishLoad(int errorCount, string finalStatus)
    {
        RefreshAutomationFields();
        StatusText = Files.Count == 0 && errorCount > 0
            ? Loc.T("Status_LoadFailedShort")
            : finalStatus;
    }

    private static string BuildFilesSourceLabel(IReadOnlyList<string> paths)
    {
        if (paths.Count == 1)
        {
            return paths[0];
        }

        var directories = paths
            .Select(path => Path.GetDirectoryName(path)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return directories.Count == 1
            ? Loc.F("FilesFromSingleFolder", directories[0], paths.Count)
            : Loc.F("FilesSelectedCount", paths.Count);
    }

    private string GetSaveSourceRoot()
    {
        if (IsFolderSource)
        {
            return FolderPath;
        }

        if (Files.Count == 0)
        {
            return string.Empty;
        }

        return Path.GetDirectoryName(Files[0].SourcePath)!;
    }

    private List<AudioFileViewModel> GetBatchTargets() =>
        BatchApplyScope == BatchApplyScope.AllInFolder
            ? Files.ToList()
            : Files.Where(file => file.IsSelected).ToList();

    private AudioFileViewModel? GetBatchValueSource() =>
        Files.FirstOrDefault(file => file.IsSelected) ?? SelectedFile;

    private void RefreshAutomationFields()
    {
        var source = GetBatchValueSource();
        BatchArtist = ApplyArtist
            ? source?.Artist ?? string.Empty
            : string.Empty;
        BatchTitle = ApplyTitle
            ? source?.Title ?? string.Empty
            : string.Empty;
        BatchAlbum = ApplyAlbum
            ? source?.Album ?? string.Empty
            : string.Empty;
        BatchYear = ApplyYear
            ? source?.Year?.ToString() ?? string.Empty
            : string.Empty;
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is not AudioFileViewModel file)
        {
            return;
        }

        if (isLoadingFiles)
        {
            return;
        }

        if (eventArgs.PropertyName == nameof(AudioFileViewModel.IsSelected))
        {
            RefreshAutomationFields();
            UpdateRenamePreview();
            return;
        }

        if (eventArgs.PropertyName == nameof(AudioFileViewModel.EditedFileName))
        {
            ValidateEditedFileName(file);
            return;
        }

        if (eventArgs.PropertyName == nameof(AudioFileViewModel.HasChanges))
        {
            if (!isApplyingBatch)
            {
                RefreshUnsavedState();
            }

            return;
        }

        if (isApplyingBatch ||
            eventArgs.PropertyName is nameof(AudioFileViewModel.ProposedFileName)
                or nameof(AudioFileViewModel.RenameError)
                or nameof(AudioFileViewModel.FileNameError))
        {
            return;
        }

        if (ApplyFilenameRename || ApplyTrackNumbering)
        {
            UpdateRenamePreview();
        }
    }

    private void RefreshUnsavedState()
    {
        OnPropertyChanged(nameof(UnsavedChangesCount));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        SaveCommand.NotifyCanExecuteChanged();
        ResetAllCommand.NotifyCanExecuteChanged();
    }

    private static bool NeedsRename(AudioFileViewModel file, SaveDialogOptions options)
    {
        if (file.HasRename)
        {
            return true;
        }

        if (!options.ApplyRename || file.RenameError is not null)
        {
            return false;
        }

        return !string.Equals(
            file.FileName,
            file.ProposedFileName,
            StringComparison.OrdinalIgnoreCase);
    }

    private bool HasApplicableRenamePreview()
    {
        if (!ApplyFilenameRename && !ApplyTrackNumbering)
        {
            return false;
        }

        RefreshRenamePreview();

        return Files.Any(file =>
            file.RenameError is null &&
            !file.HasRename &&
            !string.IsNullOrEmpty(file.ProposedFileName) &&
            !string.Equals(
                file.FileName,
                file.ProposedFileName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveSaveFileName(
        AudioFileViewModel file,
        SaveDialogOptions options)
    {
        if (file.HasRename)
        {
            return file.EditedFileName;
        }

        return options.ApplyRename ? file.ProposedFileName : file.FileName;
    }

    private static bool HasSaveBlockingError(
        AudioFileViewModel file,
        SaveDialogOptions options) =>
        NeedsRename(file, options) &&
        file.HasRename &&
        file.FileNameError is not null;

    private static void ValidateEditedFileName(AudioFileViewModel file)
    {
        if (!file.HasRename)
        {
            file.FileNameError = null;
            return;
        }

        try
        {
            FileNameValidator.ValidateFileName(file.EditedFileName);

            var destination = Path.Combine(
                Path.GetDirectoryName(file.SourcePath)!,
                file.EditedFileName);

            if (!string.Equals(
                    destination,
                    file.SourcePath,
                    StringComparison.OrdinalIgnoreCase) &&
                File.Exists(destination))
            {
                file.FileNameError = Loc.T("Error_FileExists");
                return;
            }

            file.FileNameError = null;
        }
        catch (FormatException exception)
        {
            file.FileNameError = exception.Message;
        }
    }

    private void DetectRenameConflicts()
    {
        var validFiles = Files
            .Where(file => !string.IsNullOrEmpty(file.ProposedFileName))
            .ToList();
        foreach (var file in validFiles)
        {
            file.RenameError = null;
        }

        foreach (var file in validFiles)
        {
            var destination = Path.Combine(
                Path.GetDirectoryName(file.SourcePath)!,
                file.ProposedFileName);

            if (!string.Equals(
                    destination,
                    file.SourcePath,
                    StringComparison.OrdinalIgnoreCase) &&
                File.Exists(destination))
            {
                file.RenameError = Loc.T("Error_FileExists");
            }
        }

        var duplicates = validFiles
            .Where(file => file.RenameError is null)
            .GroupBy(
                file => Path.Combine(
                    Path.GetDirectoryName(file.SourcePath)!,
                    file.ProposedFileName),
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var duplicate in duplicates)
        {
            foreach (var file in duplicate)
            {
                file.RenameError = Loc.T("Error_DuplicateNames");
            }
        }
    }

}

public sealed record FilenameTemplateOption(string Template, string Label);

public sealed record FilenameNumberFormatOption(
    FilenameNumberFormat Format,
    string Label);

internal sealed record AutomationPlan(
    AudioFileViewModel File,
    AudioMetadata Metadata,
    string? FileName);

internal sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
