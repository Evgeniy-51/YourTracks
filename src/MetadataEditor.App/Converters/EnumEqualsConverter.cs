using System.Globalization;
using System.Windows.Data;

namespace MetadataEditor.App.Converters;

public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is null || parameter is null)
        {
            return false;
        }

        var target = parameter is Enum
            ? parameter
            : Enum.Parse(value.GetType(), parameter.ToString()!);

        return value.Equals(target);
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is not true || parameter is null)
        {
            return Binding.DoNothing;
        }

        return parameter is Enum
            ? parameter
            : Enum.Parse(targetType, parameter.ToString()!);
    }
}
