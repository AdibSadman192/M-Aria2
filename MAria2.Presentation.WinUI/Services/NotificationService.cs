using Microsoft.Toolkit.Uwp.Notifications;
using MAria2.Core.Entities;
using MAria2.Core.Enums;

namespace MAria2.Presentation.WinUI.Services;

public class NotificationService
{
    public void ShowDownloadStartedNotification(Download download)
    {
        new ToastContentBuilder()
            .AddText("Download Started")
            .AddText(Path.GetFileName(download.DestinationPath))
            .Show();
    }

    public void ShowDownloadCompletedNotification(Download download)
    {
        new ToastContentBuilder()
            .AddText("Download Completed")
            .AddText(Path.GetFileName(download.DestinationPath))
            .AddButton(new ToastButton()
                .SetContent("Open Folder")
                .AddArgument("action", "open_folder")
                .AddArgument("path", Path.GetDirectoryName(download.DestinationPath)))
            .AddButton(new ToastButton()
                .SetContent("Open File")
                .AddArgument("action", "open_file")
                .AddArgument("path", download.DestinationPath))
            .Show();
    }

    public void ShowDownloadFailedNotification(Download download, string errorMessage)
    {
        new ToastContentBuilder()
            .AddText("Download Failed")
            .AddText(Path.GetFileName(download.DestinationPath))
            .AddText(errorMessage)
            .Show();
    }

    public void HandleToastActivation(ToastNotificationActivatedEventArgsCompat toastArgs)
    {
        var arguments = toastArgs.Arguments;
        
        if (arguments.TryGetValue("action", out string action) &&
            arguments.TryGetValue("path", out string path))
        {
            switch (action)
            {
                case "open_folder":
                    OpenFolder(path);
                    break;
                case "open_file":
                    OpenFile(path);
                    break;
            }
        }
    }

    private void OpenFolder(string folderPath)
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", folderPath);
        }
        catch (Exception ex)
        {
            // Log or handle error
        }
    }

    private void OpenFile(string filePath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Log or handle error
        }
    }
}
