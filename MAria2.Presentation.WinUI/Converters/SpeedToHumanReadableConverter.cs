using Microsoft.UI.Xaml.Data;

namespace MAria2.Presentation.WinUI.Converters;

public class SpeedToHumanReadableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double speedBps)
        {
            return FormatSpeed(speedBps);
        }
        return "0 B/s";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }

    private string FormatSpeed(double speedBps)
    {
        string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
        int unitIndex = 0;

        while (speedBps >= 1024 && unitIndex < units.Length - 1)
        {
            speedBps /= 1024;
            unitIndex++;
        }

        return $"{speedBps:0.##} {units[unitIndex]}";
    }
}
