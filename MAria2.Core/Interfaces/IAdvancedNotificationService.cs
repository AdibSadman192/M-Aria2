using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces
{
    public interface IAdvancedNotificationService
    {
        /// <summary>
        /// Send a notification across multiple channels
        /// </summary>
        Task SendNotificationAsync(
            NotificationRequest request, 
            NotificationChannel[] channels = null);

        /// <summary>
        /// Subscribe to specific notification types
        /// </summary>
        IAsyncEnumerable<Notification> SubscribeToNotificationsAsync(
            NotificationFilter filter);

        /// <summary>
        /// Manage notification preferences
        /// </summary>
        Task UpdateNotificationPreferencesAsync(
            NotificationPreferences preferences);

        /// <summary>
        /// Get current notification preferences
        /// </summary>
        Task<NotificationPreferences> GetNotificationPreferencesAsync();

        /// <summary>
        /// Clear notification history
        /// </summary>
        Task ClearNotificationHistoryAsync(TimeSpan? olderThan = null);
    }

    public record NotificationRequest
    {
        public string Title { get; init; }
        public string Message { get; init; }
        public NotificationType Type { get; init; }
        public NotificationSeverity Severity { get; init; }
        public object Context { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    public record Notification : NotificationRequest
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public NotificationStatus Status { get; init; }
        public NotificationChannel[] DeliveredChannels { get; init; }
    }

    public record NotificationPreferences
    {
        public Dictionary<NotificationType, NotificationChannelPreference> ChannelPreferences { get; init; }
        public Dictionary<NotificationSeverity, bool> SeverityFilters { get; init; }
        public bool DoNotDisturbMode { get; init; }
        public TimeSpan? DoNotDisturbPeriod { get; init; }
    }

    public record NotificationChannelPreference
    {
        public bool EmailEnabled { get; init; }
        public bool PushNotificationEnabled { get; init; }
        public bool DesktopNotificationEnabled { get; init; }
        public bool SlackEnabled { get; init; }
        public bool TeamsEnabled { get; init; }
    }

    public record NotificationFilter
    {
        public NotificationType[] Types { get; init; }
        public NotificationSeverity[] Severities { get; init; }
        public DateTime? StartTime { get; init; }
        public DateTime? EndTime { get; init; }
    }

    public enum NotificationType
    {
        SystemResource,
        DownloadProgress,
        DownloadCompletion,
        Error,
        Performance,
        Update,
        Security
    }

    public enum NotificationSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum NotificationStatus
    {
        Pending,
        Delivered,
        Failed,
        Read
    }

    public enum NotificationChannel
    {
        Desktop,
        Email,
        SMS,
        Slack,
        Teams,
        WebPush
    }
}
