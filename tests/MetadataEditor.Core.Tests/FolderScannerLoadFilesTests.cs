using MetadataEditor.Core.Models;
using MetadataEditor.Core.Services;

namespace MetadataEditor.Core.Tests;

public sealed class FolderScannerLoadFilesTests
{
    [Fact]
    public async Task LoadFilesAsync_LoadsSupportedFilesAndSkipsUnsupported()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var mp3 = Path.Combine(root, "track.mp3");
        var txt = Path.Combine(root, "notes.txt");
        await File.WriteAllTextAsync(mp3, "audio");
        await File.WriteAllTextAsync(txt, "text");

        var service = new FolderScanner(new StubMetadataService());
        var loaded = new List<AudioFileItem>();
        var errors = new List<string>();

        try
        {
            await service.LoadFilesAsync(
                [mp3, txt],
                new SynchronousProgress<AudioFileItem>(loaded.Add),
                new SynchronousProgress<string>(errors.Add),
                CancellationToken.None);

            Assert.Single(loaded);
            Assert.Equal(mp3, loaded[0].SourcePath);
            Assert.Single(errors);
            Assert.Contains("не поддерживается", errors[0]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StubMetadataService : IMetadataService
    {
        public AudioMetadata Read(string path) =>
            new(string.Empty, string.Empty, null, string.Empty, null, null);

        public void Write(string path, AudioMetadata metadata) =>
            throw new NotSupportedException();
    }

    private sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
