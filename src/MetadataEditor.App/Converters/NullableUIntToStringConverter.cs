using System.Globalization;
using System.Windows.Data;
using MetadataEditor.App.Localization;
namespace MetadataEditor.App.Converters;

public sealed class NullableUIntToStringConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is uint number ? number.ToString(culture) : string.Empty;

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        var text = (value as string)?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (uint.TryParse(text, NumberStyles.Integer, culture, out var number))
        {
            return number;
        }

        throw new FormatException(Loc.T("EnterNonNegativeInteger"));
    }
}
