using System.Text;
using MetadataEditor.Core.Localization;
using MetadataEditor.Core.Models;

namespace MetadataEditor.Core.Services;

public sealed class PlaylistService
{
    public string BuildContent(
        IReadOnlyList<PlaylistTrack> tracks,
        PlaylistFormat format,
        string playlistDirectory,
        bool useAbsolutePaths)
    {
        if (tracks.Count == 0)
        {
            throw new InvalidOperationException(CoreLoc.T("PlaylistNoFiles"));
        }

        var playlistRoot = Path.GetFullPath(playlistDirectory);

        return format switch
        {
            PlaylistFormat.M3U8 => BuildM3U(tracks, playlistRoot, useAbsolutePaths, utf8: true),
            PlaylistFormat.M3U => BuildM3U(tracks, playlistRoot, useAbsolutePaths, utf8: false),
            PlaylistFormat.Pls => BuildPls(tracks, playlistRoot, useAbsolutePaths),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
    }

    public async Task SaveAsync(
        string destinationPath,
        IReadOnlyList<PlaylistTrack> tracks,
        PlaylistFormat format,
        bool useAbsolutePaths,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException(CoreLoc.T("InvalidPlaylistPath"));
        Directory.CreateDirectory(directory);

        var content = BuildContent(tracks, format, directory, useAbsolutePaths);
        var encoding = format == PlaylistFormat.M3U
            ? Encoding.Default
            : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        await File.WriteAllTextAsync(destinationPath, content, encoding, cancellationToken)
            .ConfigureAwait(false);
    }

    public static string GetExtension(PlaylistFormat format) =>
        format switch
        {
            PlaylistFormat.M3U8 => ".m3u8",
            PlaylistFormat.M3U => ".m3u",
            PlaylistFormat.Pls => ".pls",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    public static string GetFilter(PlaylistFormat format) =>
        format switch
        {
            PlaylistFormat.M3U8 => CoreLoc.T("PlaylistFilterM3U8"),
            PlaylistFormat.M3U => CoreLoc.T("PlaylistFilterM3U"),
            PlaylistFormat.Pls => CoreLoc.T("PlaylistFilterPls"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    private static string BuildM3U(
        IReadOnlyList<PlaylistTrack> tracks,
        string playlistRoot,
        bool useAbsolutePaths,
        bool utf8)
    {
        _ = utf8;
        var builder = new StringBuilder();
        builder.AppendLine("#EXTM3U");

        foreach (var track in tracks)
        {
            var title = string.IsNullOrWhiteSpace(track.Title)
                ? Path.GetFileNameWithoutExtension(track.SourcePath)
                : track.Title;
            builder.Append("#EXTINF:-1,");
            builder.AppendLine(EscapeM3UTitle(title));
            builder.AppendLine(ResolvePath(track.SourcePath, playlistRoot, useAbsolutePaths));
        }

        return builder.ToString();
    }

    private static string BuildPls(
        IReadOnlyList<PlaylistTrack> tracks,
        string playlistRoot,
        bool useAbsolutePaths)
    {
        var builder = new StringBuilder();
        builder.AppendLine("[playlist]");

        for (var index = 0; index < tracks.Count; index++)
        {
            var track = tracks[index];
            var entry = index + 1;
            var title = string.IsNullOrWhiteSpace(track.Title)
                ? Path.GetFileNameWithoutExtension(track.SourcePath)
                : track.Title;

            builder.Append("File");
            builder.Append(entry);
            builder.Append('=');
            builder.AppendLine(ResolvePath(track.SourcePath, playlistRoot, useAbsolutePaths));

            builder.Append("Title");
            builder.Append(entry);
            builder.Append('=');
            builder.AppendLine(title);

            builder.Append("Length");
            builder.Append(entry);
            builder.AppendLine("=-1");
        }

        builder.Append("NumberOfEntries=");
        builder.AppendLine(tracks.Count.ToString());
        builder.AppendLine("Version=2");
        return builder.ToString();
    }

    private static string ResolvePath(
        string sourcePath,
        string playlistRoot,
        bool useAbsolutePaths)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        if (useAbsolutePaths)
        {
            return fullPath;
        }

        var relative = Path.GetRelativePath(playlistRoot, fullPath);
        if (relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return fullPath;
        }

        return relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static string EscapeM3UTitle(string title) =>
        title.Replace('\r', ' ').Replace('\n', ' ');
}
