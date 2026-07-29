using MetadataEditor.Core.Models;

namespace MetadataEditor.Core.Services;

public sealed class FolderScanner(IMetadataService metadataService) : IFolderScanner
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac", ".m4a" };

    public Task ScanAsync(
        string folderPath,
        IProgress<AudioFileItem> onFileLoaded,
        IProgress<string> onError,
        IProgress<int>? onTotalDiscovered,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var paths = Directory.EnumerateFiles(
                    folderPath,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Where(IsSupported)
                .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            onTotalDiscovered?.Report(paths.Count);

            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var item = new AudioFileItem(path, metadataService.Read(path));
                    onFileLoaded.Report(item);
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException and
                    not OutOfMemoryException)
                {
                    onError.Report($"{Path.GetFileName(path)}: {exception.Message}");
                }
            }
        }, cancellationToken);
    }

    public Task LoadFilesAsync(
        IReadOnlyList<string> filePaths,
        IProgress<AudioFileItem> onFileLoaded,
        IProgress<string> onError,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            foreach (var path in filePaths
                         .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsSupported(path))
                {
                    onError.Report($"{Path.GetFileName(path)}: формат не поддерживается.");
                    continue;
                }

                if (!File.Exists(path))
                {
                    onError.Report($"{Path.GetFileName(path)}: файл не найден.");
                    continue;
                }

                try
                {
                    var item = new AudioFileItem(path, metadataService.Read(path));
                    onFileLoaded.Report(item);
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException and
                    not OutOfMemoryException)
                {
                    onError.Report($"{Path.GetFileName(path)}: {exception.Message}");
                }
            }
        }, cancellationToken);
    }

    private static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));
}
