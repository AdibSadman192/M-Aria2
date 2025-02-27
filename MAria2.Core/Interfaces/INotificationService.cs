using System;
using System.Threading.Tasks;
using MAria2.Core.Entities;

namespace MAria2.Core.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Send a notification about a download event
    /// </summary>
    Task SendDownloadNotificationAsync(Download download, NotificationType type);

    /// <summary>
    /// Send a system-wide notification
    /// </summary>
    Task SendSystemNotificationAsync(string title, string message, SystemNotificationLevel level);

    /// <summary>
    /// Register a notification handler for specific events
    /// </summary>
    void RegisterNotificationHandler(NotificationType type, Func<Download, Task> handler);

    /// <summary>
    /// Unregister a previously registered notification handler
    /// </summary>
    void UnregisterNotificationHandler(NotificationType type);
}

public enum NotificationType
{
    DownloadStarted,
    DownloadProgress,
    DownloadCompleted,
    DownloadFailed,
    DownloadPaused,
    DownloadResumed,
    DownloadCancelled
}

public enum SystemNotificationLevel
{
    Info,
    Warning,
    Error,
    Critical
}
