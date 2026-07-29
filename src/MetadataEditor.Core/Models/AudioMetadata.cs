namespace MetadataEditor.Core.Models;

public sealed record AudioMetadata(
    string Artist,
    string Title,
    uint? TrackNumber,
    string Album,
    uint? Year,
    CoverArt? Cover);
