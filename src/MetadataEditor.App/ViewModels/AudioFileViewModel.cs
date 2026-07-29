using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MetadataEditor.Core.Models;

namespace MetadataEditor.App.ViewModels;

public partial class AudioFileViewModel : ObservableObject
{
    private AudioMetadata original;

    public AudioFileViewModel(AudioFileItem file)
    {
        SourcePath = file.SourcePath;
        FileName = file.FileName;
        EditedFileName = file.FileName;
        original = file.Metadata;
        Apply(original);
    }

    [ObservableProperty]
    private string sourcePath = string.Empty;

    [ObservableProperty]
    private string fileName = string.Empty;

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string artist = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private uint? trackNumber;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private string album = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private uint? year;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    private CoverArt? cover;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanges))]
    [NotifyPropertyChangedFor(nameof(HasRename))]
    private string editedFileName = string.Empty;

    [ObservableProperty]
    private string? fileNameError;

    [ObservableProperty]
    private string proposedFileName = string.Empty;

    [ObservableProperty]
    private string? renameError;

    public bool HasMetadataChanges =>
        Artist != original.Artist ||
        Title != original.Title ||
        TrackNumber != original.TrackNumber ||
        Album != original.Album ||
        Year != original.Year ||
        !CoversEqual(Cover, original.Cover);

    public bool HasRename =>
        !string.Equals(FileName, EditedFileName, StringComparison.OrdinalIgnoreCase);

    public bool HasChanges => HasMetadataChanges || HasRename;

    [RelayCommand]
    private void Reset()
    {
        RevertToLoadedState();
    }

    public void RevertToLoadedState()
    {
        Apply(original);
        EditedFileName = FileName;
        FileNameError = null;
        ProposedFileName = FileName;
        RenameError = null;
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(HasRename));
        OnPropertyChanged(nameof(HasMetadataChanges));
    }

    public AudioMetadata ToMetadata() =>
        new(Artist, Title, TrackNumber, Album, Year, Cover);

    public void UpdateMetadata(AudioMetadata metadata) => Apply(metadata);

    public void AcceptSaved(string path)
    {
        SourcePath = path;
        FileName = Path.GetFileName(path);
        EditedFileName = FileName;
        FileNameError = null;
        original = ToMetadata();
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(HasRename));
        OnPropertyChanged(nameof(HasMetadataChanges));
    }

    private void Apply(AudioMetadata metadata)
    {
        Artist = metadata.Artist;
        Title = metadata.Title;
        TrackNumber = metadata.TrackNumber;
        Album = metadata.Album;
        Year = metadata.Year;
        Cover = metadata.Cover;
    }

    private static bool CoversEqual(CoverArt? left, CoverArt? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left is not null &&
               right is not null &&
               left.MimeType == right.MimeType &&
               left.Data.AsSpan().SequenceEqual(right.Data);
    }
}
