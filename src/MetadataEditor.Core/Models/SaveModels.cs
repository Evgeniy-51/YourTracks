namespace MetadataEditor.Core.Models;

public enum SaveMode
{
    ModifyOriginals,
    CopyToFolder
}

public sealed record SaveFileRequest(
    string SourcePath,
    AudioMetadata Metadata,
    string ProposedFileName,
    bool MetadataChanged,
    bool ApplyRename);

public sealed record SaveRequest(
    string SourceRoot,
    IReadOnlyList<SaveFileRequest> Files,
    SaveMode Mode,
    string? DestinationFolder,
    bool ApplyRename,
    bool CreateBackups);

public sealed record SaveItemResult(
    string SourcePath,
    string? DestinationPath,
    bool Success,
    string? Error);

public sealed record SaveResult(IReadOnlyList<SaveItemResult> Items)
{
    public int SuccessfulCount => Items.Count(item => item.Success);

    public int FailedCount => Items.Count - SuccessfulCount;
}

public sealed record SaveProgress(int Completed, int Total, string FileName);
