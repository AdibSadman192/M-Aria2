using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Slack.NetStandard;
using Slack.NetStandard.WebApi;

namespace MAria2.Infrastructure.Services
{
    public class AdvancedNotificationService : IAdvancedNotificationService
    {
        private readonly ILogger<AdvancedNotificationService> _logger;
        private readonly ConcurrentDictionary<string, Notification> _notificationHistory;
        private readonly Channel<Notification> _notificationChannel;
        private readonly NotificationPreferences _defaultPreferences;

        // External notification providers
        private readonly IDesktopNotificationProvider _desktopNotifier;
        private readonly IEmailNotificationProvider _emailNotifier;
        private readonly ISlackNotificationProvider _slackNotifier;

        public AdvancedNotificationService(
            ILogger<AdvancedNotificationService> logger,
            IDesktopNotificationProvider desktopNotifier,
            IEmailNotificationProvider emailNotifier,
            ISlackNotificationProvider slackNotifier)
        {
            _logger = logger;
            _desktopNotifier = desktopNotifier;
            _emailNotifier = emailNotifier;
            _slackNotifier = slackNotifier;

            _notificationHistory = new ConcurrentDictionary<string, Notification>();
            _notificationChannel = Channel.CreateUnbounded<Notification>();

            _defaultPreferences = new NotificationPreferences
            {
                ChannelPreferences = new Dictionary<NotificationType, NotificationChannelPreference>
                {
                    { NotificationType.SystemResource, new NotificationChannelPreference 
                    { 
                        DesktopNotificationEnabled = true, 
                        SlackEnabled = true 
                    }},
                    { NotificationType.DownloadProgress, new NotificationChannelPreference 
                    { 
                        DesktopNotificationEnabled = true 
                    }}
                },
                SeverityFilters = new Dictionary<NotificationSeverity, bool>
                {
                    { NotificationSeverity.Low, true },
                    { NotificationSeverity.Medium, true },
                    { NotificationSeverity.High, true },
                    { NotificationSeverity.Critical, true }
                },
                DoNotDisturbMode = false
            };
        }

        public async Task SendNotificationAsync(
            NotificationRequest request, 
            NotificationChannel[] channels = null)
        {
            // Check Do Not Disturb
            if (_defaultPreferences.DoNotDisturbMode)
            {
                _logger.LogInformation("Notification suppressed due to Do Not Disturb mode");
                return;
            }

            // Determine channels
            channels ??= DetermineNotificationChannels(request);

            var notification = new Notification
            {
                Title = request.Title,
                Message = request.Message,
                Type = request.Type,
                Severity = request.Severity,
                Context = request.Context,
                DeliveredChannels = channels
            };

            // Store in history
            _notificationHistory[notification.Id] = notification;

            // Send via selected channels
            await SendViaChannelsAsync(notification, channels);

            // Publish to notification stream
            await _notificationChannel.Writer.WriteAsync(notification);
        }

        private NotificationChannel[] DetermineNotificationChannels(NotificationRequest request)
        {
            var channelPreference = _defaultPreferences
                .ChannelPreferences
                .GetValueOrDefault(request.Type, new NotificationChannelPreference());

            var channels = new List<NotificationChannel>();

            if (channelPreference.DesktopNotificationEnabled)
                channels.Add(NotificationChannel.Desktop);
            
            if (channelPreference.SlackEnabled)
                channels.Add(NotificationChannel.Slack);

            return channels.ToArray();
        }

        private async Task SendViaChannelsAsync(Notification notification, NotificationChannel[] channels)
        {
            var tasks = channels.Select(async channel => 
            {
                try 
                {
                    switch (channel)
                    {
                        case NotificationChannel.Desktop:
                            await _desktopNotifier.SendNotificationAsync(notification);
                            break;
                        case NotificationChannel.Slack:
                            await _slackNotifier.SendNotificationAsync(notification);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to send notification via {channel}: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);
        }

        public IAsyncEnumerable<Notification> SubscribeToNotificationsAsync(NotificationFilter filter)
        {
            // Implement filtering logic for notification stream
            return _notificationChannel.Reader.ReadAllAsync()
                .Where(n => 
                    (filter.Types == null || filter.Types.Contains(n.Type)) &&
                    (filter.Severities == null || filter.Severities.Contains(n.Severity)) &&
                    (filter.StartTime == null || n.Timestamp >= filter.StartTime) &&
                    (filter.EndTime == null || n.Timestamp <= filter.EndTime)
                );
        }

        public Task UpdateNotificationPreferencesAsync(NotificationPreferences preferences)
        {
            // Implement preference update logic
            throw new NotImplementedException();
        }

        public Task<NotificationPreferences> GetNotificationPreferencesAsync()
        {
            return Task.FromResult(_defaultPreferences);
        }

        public Task ClearNotificationHistoryAsync(TimeSpan? olderThan = null)
        {
            if (olderThan.HasValue)
            {
                var cutoffTime = DateTime.UtcNow - olderThan.Value;
                var oldNotifications = _notificationHistory
                    .Where(n => n.Value.Timestamp < cutoffTime)
                    .Select(n => n.Key)
                    .ToList();

                foreach (var key in oldNotifications)
                {
                    _notificationHistory.TryRemove(key, out _);
                }
            }
            else
            {
                _notificationHistory.Clear();
            }

            return Task.CompletedTask;
        }
    }

    // Placeholder interfaces for external notification providers
    public interface IDesktopNotificationProvider
    {
        Task SendNotificationAsync(Notification notification);
    }

    public interface IEmailNotificationProvider
    {
        Task SendNotificationAsync(Notification notification);
    }

    public interface ISlackNotificationProvider
    {
        Task SendNotificationAsync(Notification notification);
    }
}
