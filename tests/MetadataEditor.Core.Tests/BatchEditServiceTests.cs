using MetadataEditor.Core.Services;

namespace MetadataEditor.Core.Tests;

public sealed class BatchEditServiceTests
{
    [Fact]
    public void Apply_UpdatesRequestedField()
    {
        var service = new BatchEditService();
        var metadata = new MetadataEditor.Core.Models.AudioMetadata(
            "Artist",
            "Title",
            null,
            "Album",
            2020,
            null);

        var result = service.Apply(
            metadata,
            MetadataEditor.Core.Models.MetadataField.Artist,
            "New Artist");

        Assert.Equal("New Artist", result.Artist);
    }
}
