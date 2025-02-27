using Microsoft.UI.Xaml.Controls;
using MAria2.Application.Services;

namespace MAria2.Presentation.WinUI.Controls;

public sealed partial class StatusBarControl : UserControl
{
    private readonly DownloadQueueService _downloadQueueService;

    public StatusBarControl(DownloadQueueService downloadQueueService)
    {
        InitializeComponent();
        _downloadQueueService = downloadQueueService;

        // Start monitoring download status
        UpdateStatusAsync();
    }

    private async void UpdateStatusAsync()
    {
        while (true)
        {
            // Get active downloads
            var activeDownloads = _downloadQueueService.GetQueuedDownloads();

            // Calculate total download and upload speeds
            var totalDownloadSpeed = activeDownloads
                .Sum(d => d.Progress?.SpeedBps ?? 0);

            // Update UI
            DownloadSpeedTextBlock.Text = FormatSpeed(totalDownloadSpeed);
            StatusMessageTextBlock.Text = $"{activeDownloads.Count} active downloads";

            // Wait before next update
            await Task.Delay(1000);
        }
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

        return $"{speedBps:F2} {units[unitIndex]}";
    }
}
