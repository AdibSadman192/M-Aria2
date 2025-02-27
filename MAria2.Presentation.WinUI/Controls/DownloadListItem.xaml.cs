using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MAria2.Core.Entities;
using MAria2.Core.Enums;

namespace MAria2.Presentation.WinUI.Controls;

public sealed partial class DownloadListItem : UserControl
{
    public static readonly DependencyProperty DownloadProperty = 
        DependencyProperty.Register(
            nameof(Download), 
            typeof(Download), 
            typeof(DownloadListItem), 
            new PropertyMetadata(null)
        );

    public Download Download
    {
        get => (Download)GetValue(DownloadProperty);
        set => SetValue(DownloadProperty, value);
    }

    public event EventHandler<Download> PauseRequested;
    public event EventHandler<Download> ResumeRequested;
    public event EventHandler<Download> CancelRequested;

    public DownloadListItem()
    {
        InitializeComponent();
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        switch (Download.Status)
        {
            case DownloadStatus.Downloading:
                PauseRequested?.Invoke(this, Download);
                break;
            case DownloadStatus.Paused:
                ResumeRequested?.Invoke(this, Download);
                break;
            case DownloadStatus.Queued:
                // Cancel queued download
                CancelRequested?.Invoke(this, Download);
                break;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, Download);
    }
}
