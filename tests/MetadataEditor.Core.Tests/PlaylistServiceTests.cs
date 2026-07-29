using MetadataEditor.Core.Models;
using MetadataEditor.Core.Services;

namespace MetadataEditor.Core.Tests;

public sealed class PlaylistServiceTests
{
    private readonly PlaylistService service = new();

    [Fact]
    public void BuildContent_M3U8_UsesRelativePathsInSameFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var content = service.BuildContent(
            [
                new PlaylistTrack(Path.Combine(root, "a.mp3"), "Artist - A"),
                new PlaylistTrack(Path.Combine(root, "b.flac"), "B")
            ],
            PlaylistFormat.M3U8,
            root,
            useAbsolutePaths: false);

        Assert.Contains("#EXTM3U", content);
        Assert.Contains("#EXTINF:-1,Artist - A", content);
        Assert.Contains("a.mp3", content);
        Assert.Contains("b.flac", content);
        Assert.DoesNotContain(root, content);
    }

    [Fact]
    public void BuildContent_Pls_WritesEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var content = service.BuildContent(
            [new PlaylistTrack(Path.Combine(root, "track.mp3"), "Song")],
            PlaylistFormat.Pls,
            root,
            useAbsolutePaths: false);

        Assert.Contains("[playlist]", content);
        Assert.Contains("File1=track.mp3", content);
        Assert.Contains("Title1=Song", content);
        Assert.Contains("NumberOfEntries=1", content);
        Assert.Contains("Version=2", content);
    }

    [Fact]
    public void BuildContent_AbsolutePaths_UsesFullPath()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var file = Path.Combine(root, "track.mp3");
        var content = service.BuildContent(
            [new PlaylistTrack(file, null)],
            PlaylistFormat.M3U,
            root,
            useAbsolutePaths: true);

        Assert.Contains(Path.GetFullPath(file), content);
    }

    [Fact]
    public async Task SaveAsync_WritesUtf8WithoutBomForM3U8()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var playlist = Path.Combine(root, "list.m3u8");

        try
        {
            await service.SaveAsync(
                playlist,
                [new PlaylistTrack(Path.Combine(root, "a.mp3"), "A")],
                PlaylistFormat.M3U8,
                useAbsolutePaths: false,
                CancellationToken.None);

            var bytes = await File.ReadAllBytesAsync(playlist);
            Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..]);
            Assert.Contains("#EXTM3U", await File.ReadAllTextAsync(playlist));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
