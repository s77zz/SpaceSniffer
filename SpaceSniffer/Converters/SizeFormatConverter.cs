using System.Globalization;
using System.Windows.Data;

namespace SpaceSniffer.Converters;

[ValueConversion(typeof(long), typeof(string))]
public class SizeFormatConverter : IValueConverter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes && bytes >= 0)
        {
            double size = bytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < Units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            return $"{size:0.##} {Units[unitIndex]}";
        }
        return "0 B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
