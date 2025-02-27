using Microsoft.UI.Xaml.Data;
using MAria2.Core.Enums;

namespace MAria2.Presentation.WinUI.Converters;

public class DownloadStatusToActionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DownloadStatus status)
        {
            return status switch
            {
                DownloadStatus.Downloading => "Pause",
                DownloadStatus.Paused => "Resume",
                DownloadStatus.Queued => "Cancel",
                DownloadStatus.Completed => "Open",
                DownloadStatus.Failed => "Retry",
                DownloadStatus.Canceled => "Restart",
                _ => "Unknown"
            };
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
