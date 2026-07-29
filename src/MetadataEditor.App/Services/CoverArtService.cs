using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MetadataEditor.App.Localization;
using MetadataEditor.Core.Models;

namespace MetadataEditor.App.Services;

public sealed class CoverArtService
{
    public const int MaxSourceFileMegabytes = 5;
    public const int MaxSourceFileBytes = MaxSourceFileMegabytes * 1024 * 1024;
    public const int MaxPixelDimension = 1400;
    private const int JpegQuality = 85;
    private const long MaxDecodedPixels = 16_000_000;

    public CoverArt LoadFromFile(string path)
    {
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(path);
        }

        if (fileInfo.Length > MaxSourceFileBytes)
        {
            throw new FormatException(
                Loc.F("Error_CoverFileTooLarge", MaxSourceFileMegabytes));
        }

        using var stream = File.OpenRead(path);
        return ProcessStream(stream);
    }

    private static CoverArt ProcessStream(Stream stream)
    {
        if (stream.Length >= 3)
        {
            Span<byte> header = stackalloc byte[8];
            var read = stream.Read(header);
            stream.Position = 0;
            if (read < 3 || (!LooksLikeJpeg(header) && !LooksLikePng(header)))
            {
                throw new FormatException(Loc.T("Error_CoverUnsupportedFormat"));
            }
        }

        BitmapFrame sourceFrame;
        try
        {
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);
            sourceFrame = decoder.Frames[0];
            sourceFrame.Freeze();
        }
        catch
        {
            throw new FormatException(Loc.T("Error_CoverInvalidImage"));
        }

        var pixelCount = (long)sourceFrame.PixelWidth * sourceFrame.PixelHeight;
        if (pixelCount <= 0 || pixelCount > MaxDecodedPixels)
        {
            throw new FormatException(Loc.T("Error_CoverInvalidImage"));
        }

        var frame = ResizeIfNeeded(sourceFrame);
        using var output = new MemoryStream();
        var encoder = new JpegBitmapEncoder { QualityLevel = JpegQuality };
        encoder.Frames.Add(frame);
        encoder.Save(output);
        return new CoverArt(output.ToArray(), "image/jpeg");
    }

    private static BitmapFrame ResizeIfNeeded(BitmapFrame source)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        if (width <= MaxPixelDimension && height <= MaxPixelDimension)
        {
            return source;
        }

        var scale = Math.Min(
            (double)MaxPixelDimension / width,
            (double)MaxPixelDimension / height);
        var targetWidth = Math.Max(1, (int)Math.Round(width * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(height * scale));

        var scaled = new TransformedBitmap(
            source,
            new ScaleTransform(
                targetWidth / (double)width,
                targetHeight / (double)height));
        scaled.Freeze();

        var frame = BitmapFrame.Create(scaled);
        frame.Freeze();
        return frame;
    }

    private static bool LooksLikeJpeg(ReadOnlySpan<byte> data) =>
        data.Length >= 3 &&
        data[0] == 0xFF &&
        data[1] == 0xD8 &&
        data[2] == 0xFF;

    private static bool LooksLikePng(ReadOnlySpan<byte> data) =>
        data.Length >= 8 &&
        data[0] == 0x89 &&
        data[1] == 0x50 &&
        data[2] == 0x4E &&
        data[3] == 0x47 &&
        data[4] == 0x0D &&
        data[5] == 0x0A &&
        data[6] == 0x1A &&
        data[7] == 0x0A;
}
