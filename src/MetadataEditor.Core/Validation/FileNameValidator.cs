using System.Text;
using System.Text.RegularExpressions;
using MetadataEditor.Core.Localization;

namespace MetadataEditor.Core.Validation;

public static partial class FileNameValidator
{
    private static readonly HashSet<string> ReservedNames = BuildReservedNames();

    public static string Normalize(string input, string requiredExtension)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new FormatException(CoreLoc.T("FileNameEmpty"));
        }

        var sanitized = Sanitize(input.Trim());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new FormatException(CoreLoc.T("FileNameEmpty"));
        }

        if (!sanitized.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
        {
            sanitized += requiredExtension;
        }

        ValidateFileName(sanitized);
        return sanitized;
    }

    public static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new FormatException(CoreLoc.T("FileNameEmpty"));
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new FormatException(CoreLoc.T("FileNameInvalidChars"));
        }

        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            throw new FormatException(CoreLoc.T("FileNameContainsPath"));
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            throw new FormatException(CoreLoc.T("FileNameOnlyExtension"));
        }

        if (ReservedNames.Contains(baseName))
        {
            throw new FormatException(CoreLoc.F("FileNameReserved", baseName));
        }
    }

    internal static string Sanitize(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (!invalidChars.Contains(character))
            {
                builder.Append(character);
            }
        }

        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim().TrimEnd('.', ' ');
    }

    private static HashSet<string> BuildReservedNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL"
        };

        for (var index = 1; index <= 9; index++)
        {
            names.Add($"COM{index}");
            names.Add($"LPT{index}");
        }

        return names;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
