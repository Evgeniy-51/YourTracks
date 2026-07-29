using MetadataEditor.Core.Models;
using MetadataEditor.Core.Services;

namespace MetadataEditor.Core.Tests;

public sealed class FilenameNumberingServiceTests
{
    private readonly FilenameNumberingService service = new();

    [Theory]
    [InlineData("song.mp3", "01-song.mp3")]
    [InlineData("07. song.mp3", "01-song.mp3")]
    [InlineData("003 - song.flac", "01-song.flac")]
    [InlineData("01- 01-song.m4a", "01-song.m4a")]
    [InlineData("2024 live.mp3", "01-2024 live.mp3")]
    public void Apply_ReplacesLeadingNumbering(
        string fileName,
        string expected)
    {
        var result = service.Apply(
            fileName,
            1,
            FilenameNumberFormat.TwoDigitsDash);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(FilenameNumberFormat.TwoDigitsSpace, "03 song.mp3")]
    [InlineData(FilenameNumberFormat.TwoDigitsDotSpace, "03. song.mp3")]
    [InlineData(FilenameNumberFormat.TwoDigitsDash, "03-song.mp3")]
    [InlineData(FilenameNumberFormat.ThreeDigitsSpace, "003 song.mp3")]
    public void Apply_UsesSelectedFormat(
        FilenameNumberFormat format,
        string expected)
    {
        Assert.Equal(expected, service.Apply("song.mp3", 3, format));
    }
}
