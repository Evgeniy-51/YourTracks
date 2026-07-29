namespace MetadataEditor.Core.Models;

public enum PlaylistFormat
{
    M3U8,
    M3U,
    Pls
}

public sealed record PlaylistTrack(
    string SourcePath,
    string? Title);
