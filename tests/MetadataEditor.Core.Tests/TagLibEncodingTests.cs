using MetadataEditor.Core.Services;

namespace MetadataEditor.Core.Tests;

public sealed class TagLibEncodingTests
{
    [Fact]
    public void FixMislabelledEncoding_reinterprets_latin1_as_windows1251()
    {
        // "Агата" in CP1251, as TagLib would expose after true Latin-1 decode
        var mojibake = "Àãàòà";

        var fixedText = TagLibMetadataService.FixMislabelledEncoding(mojibake);

        Assert.Equal("Агата", fixedText);
    }

    [Fact]
    public void FixMislabelledEncoding_keeps_real_cyrillic()
    {
        const string text = "Агата Кристи";

        var fixedText = TagLibMetadataService.FixMislabelledEncoding(text);

        Assert.Equal(text, fixedText);
    }

    [Fact]
    public void FixMislabelledEncoding_keeps_ascii()
    {
        const string text = "Rock'n'Roll";

        var fixedText = TagLibMetadataService.FixMislabelledEncoding(text);

        Assert.Equal(text, fixedText);
    }
}
