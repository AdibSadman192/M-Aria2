using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MAria2.Core.Entities;
using MAria2.Application.Services;
using System.Collections.ObjectModel;

namespace MAria2.Presentation.WinUI.Pages;

public sealed partial class DownloadsPage : Page
{
    private readonly DownloadService _downloadService;
    private readonly DownloadQueueService _downloadQueueService;

    public ObservableCollection<Download> DownloadItems { get; } = new();

    public DownloadsPage(
        DownloadService downloadService,
        DownloadQueueService downloadQueueService)
    {
        InitializeComponent();
        
        _downloadService = downloadService;
        _downloadQueueService = downloadQueueService;

        // Load existing downloads
        LoadDownloads();
    }

    private async void LoadDownloads()
    {
        var activeDownloads = _downloadQueueService.GetQueuedDownloads();
        foreach (var download in activeDownloads)
        {
            DownloadItems.Add(download);
        }
    }

    private async void NewDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        // Show download creation dialog
        var dialog = new ContentDialog
        {
            Title = "New Download",
            Content = new NewDownloadDialog(),
            PrimaryButtonText = "Start Download",
            CloseButtonText = "Cancel"
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var newDownloadDialog = dialog.Content as NewDownloadDialog;
            var download = await _downloadService.StartDownloadAsync(
                newDownloadDialog.DownloadUrl, 
                newDownloadDialog.DestinationPath
            );

            DownloadItems.Add(download);
        }
    }

    private void PauseAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var download in DownloadItems)
        {
            _downloadService.PauseDownloadAsync(download.Id);
        }
    }

    private void ResumeAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var download in DownloadItems)
        {
            _downloadService.ResumeDownloadAsync(download.Id);
        }
    }

    private void DownloadListItem_PauseRequested(object sender, Download download)
    {
        _downloadService.PauseDownloadAsync(download.Id);
    }

    private void DownloadListItem_ResumeRequested(object sender, Download download)
    {
        _downloadService.ResumeDownloadAsync(download.Id);
    }

    private void DownloadListItem_CancelRequested(object sender, Download download)
    {
        _downloadService.CancelDownloadAsync(download.Id);
        DownloadItems.Remove(download);
    }
}
