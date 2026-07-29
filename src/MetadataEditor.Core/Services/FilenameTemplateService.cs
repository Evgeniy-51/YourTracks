using System.Text.RegularExpressions;
using MetadataEditor.Core.Localization;
using MetadataEditor.Core.Models;
using MetadataEditor.Core.Validation;

namespace MetadataEditor.Core.Services;

public sealed partial class FilenameTemplateService
{
    private static readonly HashSet<string> ReservedNames = BuildReservedNames();

    public string Render(string template, AudioMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new FormatException(CoreLoc.T("TemplateEmpty"));
        }

        var rendered = TokenRegex().Replace(template, match => RenderToken(match, metadata));
        if (rendered.Contains('{') || rendered.Contains('}'))
        {
            throw new FormatException(CoreLoc.T("TemplateUnknownToken"));
        }

        rendered = Sanitize(rendered);
        if (string.IsNullOrWhiteSpace(rendered))
        {
            throw new FormatException(CoreLoc.T("RenderedNameEmpty"));
        }

        if (ReservedNames.Contains(rendered))
        {
            throw new FormatException(CoreLoc.F("RenderedNameReserved", rendered));
        }

        return rendered;
    }

    private static string RenderToken(Match match, AudioMetadata metadata)
    {
        var name = match.Groups["name"].Value.ToLowerInvariant();
        var format = match.Groups["format"].Success
            ? match.Groups["format"].Value
            : null;

        return name switch
        {
            "artist" => RequireNoFormat(name, format, metadata.Artist),
            "title" => RequireNoFormat(name, format, metadata.Title),
            "album" => RequireNoFormat(name, format, metadata.Album),
            "year" => RequireNoFormat(name, format, metadata.Year?.ToString() ?? string.Empty),
            "track" => metadata.TrackNumber?.ToString(format) ?? string.Empty,
            _ => throw new FormatException(CoreLoc.F("UnknownToken", name))
        };
    }

    private static string RequireNoFormat(string token, string? format, string value)
    {
        if (format is not null)
        {
            throw new FormatException(CoreLoc.F("TokenFormatNotSupported", token));
        }

        return value;
    }

    private static string Sanitize(string value) => FileNameValidator.Sanitize(value);

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

    [GeneratedRegex(@"\{(?<name>[a-z]+)(?::(?<format>[^{}]+))?\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

}
