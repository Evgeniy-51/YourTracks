using System.Text;
using MetadataEditor.Core.Models;

namespace MetadataEditor.Core.Services;

public sealed class TagLibMetadataService : IMetadataService
{
    private static readonly Encoding Latin1;
    private static readonly Encoding Windows1251;

    static TagLibMetadataService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Latin1 = Encoding.GetEncoding(28591); // ISO-8859-1 — preserves bytes 0–255
        Windows1251 = Encoding.GetEncoding(1251);

        // Do NOT use UseBrokenLatin1Behavior: on Win11 with UTF-8 system locale
        // Encoding.Default is UTF-8 and CP1251 bytes become ''.
        TagLib.ByteVector.UseBrokenLatin1Behavior = false;

        TagLib.Id3v2.Tag.DefaultEncoding = TagLib.StringType.UTF16;
        TagLib.Id3v2.Tag.ForceDefaultEncoding = true;
    }

    public AudioMetadata Read(string path)
    {
        using var file = TagLib.File.Create(path);
        var tag = file.Tag;
        var picture = tag.Pictures.FirstOrDefault();
        var cover = picture is null
            ? null
            : new CoverArt(picture.Data.Data, picture.MimeType ?? "application/octet-stream");

        return new AudioMetadata(
            FixMislabelledEncoding(tag.Performers.FirstOrDefault() ?? string.Empty),
            FixMislabelledEncoding(tag.Title ?? string.Empty),
            tag.Track == 0 ? null : tag.Track,
            FixMislabelledEncoding(tag.Album ?? string.Empty),
            tag.Year == 0 ? null : tag.Year,
            cover);
    }

    public void Write(string path, AudioMetadata metadata)
    {
        using var file = TagLib.File.Create(path);
        var tag = file.Tag;

        tag.Performers = string.IsNullOrWhiteSpace(metadata.Artist)
            ? []
            : [metadata.Artist];
        tag.Title = metadata.Title;
        tag.Track = metadata.TrackNumber ?? 0;
        tag.Album = metadata.Album;
        tag.Year = metadata.Year ?? 0;
        tag.Pictures = metadata.Cover is null
            ? []
            :
            [
                new TagLib.Picture(new TagLib.ByteVector(metadata.Cover.Data))
                {
                    Type = TagLib.PictureType.FrontCover,
                    MimeType = metadata.Cover.MimeType
                }
            ];

        file.Save();
    }

    /// <summary>
    /// ID3 frames marked Latin-1 often store Windows-1251 bytes.
    /// TagLib maps those to U+00xx; reinterpret as CP1251 when that yields Cyrillic.
    /// Leaves UTF-8/UTF-16 tags (already Cyrillic) untouched.
    /// </summary>
    public static string FixMislabelledEncoding(string value)
    {
        if (string.IsNullOrEmpty(value) || ContainsCyrillic(value))
        {
            return value;
        }

        var bytes = Latin1.GetBytes(value);
        var converted = Windows1251.GetString(bytes);
        return ContainsCyrillic(converted) ? converted : value;
    }

    private static bool ContainsCyrillic(string value)
    {
        foreach (var c in value)
        {
            if (c is >= '\u0400' and <= '\u04FF')
            {
                return true;
            }
        }

        return false;
    }
}
