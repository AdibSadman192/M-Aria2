using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MAria2.Core.Interfaces;
using MAria2.Core.Entities;

namespace MAria2.Application.Services;

public class NotificationService : INotificationService, IDisposable
{
    private readonly ILoggingService _loggingService;
    private readonly ConcurrentDictionary<NotificationType, Func<Download, Task>> _notificationHandlers;

    // Windows-specific notification components
    private readonly bool _isWindows;

    public NotificationService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
        _notificationHandlers = new ConcurrentDictionary<NotificationType, Func<Download, Task>>();
        _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    public async Task SendDownloadNotificationAsync(Download download, NotificationType type)
    {
        try 
        {
            // Log the notification
            _loggingService.LogInformation(
                $"Notification: {type} for download {download.Id} - {download.Url}"
            );

            // Invoke registered handler if exists
            if (_notificationHandlers.TryGetValue(type, out var handler))
            {
                await handler(download);
            }

            // Platform-specific notification dispatch
            switch (type)
            {
                case NotificationType.DownloadCompleted:
                    await SendWindowsToastNotificationAsync(
                        "Download Completed", 
                        $"'{download.FileName}' has finished downloading."
                    );
                    break;
                
                case NotificationType.DownloadFailed:
                    await SendWindowsToastNotificationAsync(
                        "Download Failed", 
                        $"'{download.FileName}' download failed.",
                        SystemNotificationLevel.Error
                    );
                    break;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Notification dispatch failed: {ex.Message}");
        }
    }

    public async Task SendSystemNotificationAsync(
        string title, 
        string message, 
        SystemNotificationLevel level = SystemNotificationLevel.Info)
    {
        try 
        {
            _loggingService.LogInformation($"System Notification: {title} - {message}");

            if (_isWindows)
            {
                await SendWindowsToastNotificationAsync(title, message, level);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"System notification failed: {ex.Message}");
        }
    }

    private async Task SendWindowsToastNotificationAsync(
        string title, 
        string message, 
        SystemNotificationLevel level = SystemNotificationLevel.Info)
    {
        if (!_isWindows) return;

        try 
        {
            // Windows-specific toast notification using Windows.UI.Notifications
            var template = Windows.UI.Notifications.ToastTemplateType.ToastText02;
            var toastXml = Windows.UI.Notifications.ToastNotificationManager.GetTemplateContent(template);

            var toastTextElements = toastXml.GetElementsByTagName("text");
            toastTextElements[0].AppendChild(toastXml.CreateTextNode(title));
            toastTextElements[1].AppendChild(toastXml.CreateTextNode(message));

            var toast = new Windows.UI.Notifications.ToastNotification(toastXml);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("M-Aria2").Show(toast);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Windows toast notification failed: {ex.Message}");
        }
    }

    public void RegisterNotificationHandler(
        NotificationType type, 
        Func<Download, Task> handler)
    {
        _notificationHandlers[type] = handler;
        _loggingService.LogInformation($"Registered handler for {type} notification");
    }

    public void UnregisterNotificationHandler(NotificationType type)
    {
        _notificationHandlers.TryRemove(type, out _);
        _loggingService.LogInformation($"Unregistered handler for {type} notification");
    }

    // Advanced notification routing
    public class NotificationRouter
    {
        private readonly INotificationService _notificationService;
        private readonly ILoggingService _loggingService;

        public NotificationRouter(
            INotificationService notificationService, 
            ILoggingService loggingService)
        {
            _notificationService = notificationService;
            _loggingService = loggingService;
        }

        public async Task RouteDownloadNotificationAsync(
            Download download, 
            NotificationType type,
            bool sendSystemNotification = true)
        {
            try 
            {
                // Dispatch download-specific notification
                await _notificationService.SendDownloadNotificationAsync(download, type);

                // Optional system-wide notification
                if (sendSystemNotification)
                {
                    string title = type switch
                    {
                        NotificationType.DownloadCompleted => "Download Complete",
                        NotificationType.DownloadFailed => "Download Failed",
                        _ => "Download Update"
                    };

                    string message = $"{download.FileName}: {type}";
                    
                    var level = type == NotificationType.DownloadFailed 
                        ? SystemNotificationLevel.Error 
                        : SystemNotificationLevel.Info;

                    await _notificationService.SendSystemNotificationAsync(title, message, level);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError($"Notification routing failed: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _notificationHandlers.Clear();
    }
}
