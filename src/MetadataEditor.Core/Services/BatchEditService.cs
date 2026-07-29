using MetadataEditor.Core.Localization;
using MetadataEditor.Core.Models;

namespace MetadataEditor.Core.Services;

public sealed class BatchEditService
{
    public AudioMetadata Apply(
        AudioMetadata metadata,
        MetadataField field,
        string value)
    {
        return field switch
        {
            MetadataField.Artist => metadata with { Artist = value },
            MetadataField.Title => metadata with { Title = value },
            MetadataField.Album => metadata with { Album = value },
            MetadataField.TrackNumber => metadata with
            {
                TrackNumber = ParseNumber(value, CoreLoc.T("FieldTrackNumber"))
            },
            MetadataField.Year => metadata with
            {
                Year = ParseNumber(value, CoreLoc.T("FieldYear"))
            },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
    }

    private static uint? ParseNumber(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!uint.TryParse(value, out var number) || number == 0)
        {
            throw new FormatException(CoreLoc.F("FieldMustBePositive", fieldName));
        }

        return number;
    }
}
