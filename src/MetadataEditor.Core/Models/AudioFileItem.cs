namespace MetadataEditor.Core.Models;

public sealed record AudioFileItem(string SourcePath, AudioMetadata Metadata)
{
    public string FileName => Path.GetFileName(SourcePath);
}
