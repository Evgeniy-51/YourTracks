using System.Text.RegularExpressions;
using MetadataEditor.Core.Localization;
using MetadataEditor.Core.Models;

namespace MetadataEditor.Core.Services;

public sealed partial class FilenameNumberingService
{
    public string Apply(string fileName, uint number, FilenameNumberFormat format)
    {
        if (number == 0)
        {
            throw new FormatException(CoreLoc.T("NumberMustBePositive"));
        }

        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var stemWithoutNumber = StripLeadingNumbers(stem);

        if (string.IsNullOrWhiteSpace(stemWithoutNumber))
        {
            throw new FormatException(CoreLoc.T("NameEmptyAfterStripNumber"));
        }

        return FormatPrefix(number, format) + stemWithoutNumber + extension;
    }

    public static string StripLeadingNumbers(string stem) =>
        LeadingNumbersRegex().Replace(stem, string.Empty);

    public static string FormatPrefix(uint number, FilenameNumberFormat format) =>
        format switch
        {
            FilenameNumberFormat.TwoDigitsSpace => $"{number:00} ",
            FilenameNumberFormat.TwoDigitsDotSpace => $"{number:00}. ",
            FilenameNumberFormat.TwoDigitsDash => $"{number:00}-",
            FilenameNumberFormat.ThreeDigitsSpace => $"{number:000} ",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    [GeneratedRegex(
        @"^(?:\d{1,3}(?:\s*[-._]\s*|\s+))+",
        RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumbersRegex();
}
