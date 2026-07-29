using MetadataEditor.Core.Models;

namespace MetadataEditor.Core.Services;

public interface IMetadataService
{
    AudioMetadata Read(string path);

    void Write(string path, AudioMetadata metadata);
}
