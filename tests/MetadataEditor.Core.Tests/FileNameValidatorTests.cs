using MetadataEditor.Core.Validation;

namespace MetadataEditor.Core.Tests;

public sealed class FileNameValidatorTests
{
    [Fact]
    public void Normalize_AppendsMissingExtension()
    {
        var result = FileNameValidator.Normalize("My Track", ".flac");

        Assert.Equal("My Track.flac", result);
    }

    [Fact]
    public void Normalize_RemovesInvalidCharacters()
    {
        var result = FileNameValidator.Normalize("What: Is / This?.mp3", ".mp3");

        Assert.Equal("What Is This.mp3", result);
    }

    [Theory]
    [InlineData("CON.mp3")]
    [InlineData("lpt1.flac")]
    public void ValidateFileName_RejectsReservedNames(string fileName)
    {
        Assert.Throws<FormatException>(() => FileNameValidator.ValidateFileName(fileName));
    }
}
