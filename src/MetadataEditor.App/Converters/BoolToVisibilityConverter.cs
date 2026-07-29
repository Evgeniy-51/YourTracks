using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MetadataEditor.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        var visible = value is true;
        if (string.Equals(parameter as string, "Inverse", StringComparison.Ordinal))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        throw new NotSupportedException();
}
