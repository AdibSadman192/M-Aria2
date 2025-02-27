using Microsoft.UI.Xaml.Data;

namespace MAria2.Presentation.WinUI.Converters;

public class BytesToHumanReadableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is long bytes)
        {
            return FormatBytes(bytes);
        }
        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }

    private string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        int unitIndex = 0;
        double bytesDouble = bytes;

        while (bytesDouble >= 1024 && unitIndex < units.Length - 1)
        {
            bytesDouble /= 1024;
            unitIndex++;
        }

        return $"{bytesDouble:0.##} {units[unitIndex]}";
    }
}
