using MetadataEditor.Core.Models;
using MetadataEditor.Core.Services;

namespace MetadataEditor.Core.Tests;

public sealed class FilenameTemplateServiceTests
{
    private readonly FilenameTemplateService service = new();

    [Fact]
    public void Render_FormatsTrackAndSubstitutesMetadata()
    {
        var metadata = CreateMetadata(
            artist: "Massive Attack",
            title: "Teardrop",
            trackNumber: 3);

        var result = service.Render(
            "{track:00} - {artist} - {title}",
            metadata);

        Assert.Equal("03 - Massive Attack - Teardrop", result);
    }

    [Fact]
    public void Render_RemovesInvalidWindowsCharacters()
    {
        var metadata = CreateMetadata(title: "What: Is / This?");

        var result = service.Render("{title}", metadata);

        Assert.Equal("What Is This", result);
    }

    [Fact]
    public void Render_RejectsUnknownToken()
    {
        var metadata = CreateMetadata();

        var exception = Assert.Throws<FormatException>(
            () => service.Render("{genre}", metadata));

        Assert.Contains("genre", exception.Message);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("lpt1")]
    [InlineData("COM9")]
    public void Render_RejectsReservedWindowsNames(string title)
    {
        var metadata = CreateMetadata(title: title);

        Assert.Throws<FormatException>(() => service.Render("{title}", metadata));
    }

    private static AudioMetadata CreateMetadata(
        string artist = "",
        string title = "",
        uint? trackNumber = null) =>
        new(artist, title, trackNumber, string.Empty, null, null);
}
