using MetadataEditor.Core.Models;

namespace MetadataEditor.Core.Services;

public interface IFolderScanner
{
    Task ScanAsync(
        string folderPath,
        IProgress<AudioFileItem> onFileLoaded,
        IProgress<string> onError,
        IProgress<int>? onTotalDiscovered,
        CancellationToken cancellationToken);

    Task LoadFilesAsync(
        IReadOnlyList<string> filePaths,
        IProgress<AudioFileItem> onFileLoaded,
        IProgress<string> onError,
        CancellationToken cancellationToken);
}
