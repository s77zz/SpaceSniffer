using System.Globalization;
using System.Windows.Data;
using SpaceSniffer.Helpers;

namespace SpaceSniffer.Converters;

[ValueConversion(typeof(long), typeof(string))]
public class SizeFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes && bytes >= 0)
            return FormatHelper.FormatSize(bytes);
        return "0 B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
