using MetadataEditor.Core.Models;
using MetadataEditor.Core.Services;

namespace MetadataEditor.Core.Tests;

public sealed class FileSaveServiceTests
{
    private readonly FileSaveService service = new(new StubMetadataService());

    [Fact]
    public void BuildPlan_CopiesToFlatDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, "output");
        var source = Path.Combine(root, "disc-1", "track.flac");
        var request = CreateRequest(
            root,
            source,
            SaveMode.CopyToFolder,
            destination,
            "01 - Track.flac");

        var operation = Assert.Single(service.BuildPlan(request));

        Assert.Equal(
            Path.Combine(destination, "01 - Track.flac"),
            operation.DestinationPath);
    }

    [Fact]
    public void BuildPlan_RejectsDuplicateDestinations()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, "output");
        var files = new[]
        {
            CreateFile(Path.Combine(root, "one.mp3"), "same.mp3"),
            CreateFile(Path.Combine(root, "two.mp3"), "same.mp3")
        };
        var request = new SaveRequest(
            root,
            files,
            SaveMode.CopyToFolder,
            destination,
            ApplyRename: true,
            CreateBackups: false);

        Assert.Throws<InvalidOperationException>(() => service.BuildPlan(request));
    }

    [Fact]
    public void BuildPlan_RejectsExistingCopyDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, "output");
        Directory.CreateDirectory(destination);
        var existing = Path.Combine(destination, "track.mp3");
        File.WriteAllText(existing, "existing");

        try
        {
            var request = CreateRequest(
                root,
                Path.Combine(root, "track.mp3"),
                SaveMode.CopyToFolder,
                destination,
                "track.mp3");

            Assert.Throws<InvalidOperationException>(() => service.BuildPlan(request));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_CopyModeLeavesOriginalUntouched()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, "output");
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "track.mp3");
        await File.WriteAllTextAsync(source, "audio");
        var serviceWithWriter = new FileSaveService(new AppendingMetadataService());

        try
        {
            var request = CreateRequest(
                root,
                source,
                SaveMode.CopyToFolder,
                destination,
                "renamed.mp3");

            var result = await serviceWithWriter.SaveAsync(
                request,
                progress: null,
                CancellationToken.None);

            Assert.Equal("audio", await File.ReadAllTextAsync(source));
            Assert.Equal(
                "audio-tags",
                await File.ReadAllTextAsync(Path.Combine(destination, "renamed.mp3")));
            Assert.Equal(1, result.SuccessfulCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ModifyModeCreatesBackupBeforeReplacement()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "track.flac");
        await File.WriteAllTextAsync(source, "audio");
        var serviceWithWriter = new FileSaveService(new AppendingMetadataService());
        var request = new SaveRequest(
            root,
            [CreateFile(source, "ignored.flac", applyRename: false)],
            SaveMode.ModifyOriginals,
            DestinationFolder: null,
            ApplyRename: false,
            CreateBackups: true);

        try
        {
            var result = await serviceWithWriter.SaveAsync(
                request,
                progress: null,
                CancellationToken.None);

            Assert.Equal("audio-tags", await File.ReadAllTextAsync(source));
            Assert.Equal("audio", await File.ReadAllTextAsync(source + ".bak"));
            Assert.Equal(1, result.SuccessfulCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static SaveRequest CreateRequest(
        string root,
        string source,
        SaveMode mode,
        string? destination,
        string proposedName) =>
        new(
            root,
            [CreateFile(source, proposedName)],
            mode,
            destination,
            ApplyRename: true,
            CreateBackups: false);

    private static SaveFileRequest CreateFile(
        string source,
        string proposedName,
        bool applyRename = true) =>
        new(
            source,
            new AudioMetadata(string.Empty, string.Empty, null, string.Empty, null, null),
            proposedName,
            MetadataChanged: true,
            ApplyRename: applyRename);

    private sealed class StubMetadataService : IMetadataService
    {
        public AudioMetadata Read(string path) => throw new NotSupportedException();

        public void Write(string path, AudioMetadata metadata) =>
            throw new NotSupportedException();
    }

    private sealed class AppendingMetadataService : IMetadataService
    {
        public AudioMetadata Read(string path) => throw new NotSupportedException();

        public void Write(string path, AudioMetadata metadata) =>
            File.AppendAllText(path, "-tags");
    }
}
