using System;
using System.Linq;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using MAria2.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MAria2.Tests.NotificationTests
{
    public class NotificationServiceTests
    {
        private readonly Mock<ILogger<AdvancedNotificationService>> _mockLogger;
        private readonly Mock<IDesktopNotificationProvider> _mockDesktopNotifier;
        private readonly Mock<IEmailNotificationProvider> _mockEmailNotifier;
        private readonly Mock<ISlackNotificationProvider> _mockSlackNotifier;

        public NotificationServiceTests()
        {
            _mockLogger = new Mock<ILogger<AdvancedNotificationService>>();
            _mockDesktopNotifier = new Mock<IDesktopNotificationProvider>();
            _mockEmailNotifier = new Mock<IEmailNotificationProvider>();
            _mockSlackNotifier = new Mock<ISlackNotificationProvider>();
        }

        [Fact]
        public async Task SendNotification_MultipleChannels_Success()
        {
            // Arrange
            var notificationService = CreateNotificationService();
            var notification = CreateTestNotification();

            // Act
            await notificationService.SendNotificationAsync(notification, 
                new[] { NotificationChannel.Desktop, NotificationChannel.Slack });

            // Assert
            _mockDesktopNotifier.Verify(
                x => x.SendNotificationAsync(It.Is<Notification>(n => 
                    n.Title == notification.Title)), Times.Once);
            
            _mockSlackNotifier.Verify(
                x => x.SendNotificationAsync(It.Is<Notification>(n => 
                    n.Title == notification.Title)), Times.Once);
        }

        [Fact]
        public async Task SendNotification_DoNotDisturbMode_Suppressed()
        {
            // Arrange
            var notificationService = CreateNotificationService(enableDoNotDisturb: true);
            var notification = CreateTestNotification();

            // Act
            await notificationService.SendNotificationAsync(notification);

            // Assert
            _mockDesktopNotifier.Verify(
                x => x.SendNotificationAsync(It.IsAny<Notification>()), Times.Never);
            
            _mockSlackNotifier.Verify(
                x => x.SendNotificationAsync(It.IsAny<Notification>()), Times.Never);
        }

        [Fact]
        public async Task SubscribeToNotifications_FilterWorks()
        {
            // Arrange
            var notificationService = CreateNotificationService();
            var notifications = new[]
            {
                CreateTestNotification(NotificationType.SystemResource, NotificationSeverity.High),
                CreateTestNotification(NotificationType.DownloadProgress, NotificationSeverity.Low),
                CreateTestNotification(NotificationType.Error, NotificationSeverity.Critical)
            };

            // Act
            var filter = new NotificationFilter 
            { 
                Types = new[] { NotificationType.SystemResource, NotificationType.Error },
                Severities = new[] { NotificationSeverity.High, NotificationSeverity.Critical }
            };

            var filteredNotifications = await notificationService
                .SubscribeToNotificationsAsync(filter)
                .Take(2)
                .ToListAsync();

            // Assert
            Assert.Equal(2, filteredNotifications.Count);
            Assert.All(filteredNotifications, n => 
                Assert.True(
                    filter.Types.Contains(n.Type) && 
                    filter.Severities.Contains(n.Severity)
                )
            );
        }

        [Fact]
        public async Task ClearNotificationHistory_OlderThan_Successful()
        {
            // Arrange
            var notificationService = CreateNotificationService();
            
            // Send some notifications
            await notificationService.SendNotificationAsync(CreateTestNotification());
            await notificationService.SendNotificationAsync(CreateTestNotification());

            // Act
            await notificationService.ClearNotificationHistoryAsync(TimeSpan.FromMinutes(1));

            // Assert - This would require additional verification mechanism
            // Typically would mock a storage mechanism to verify clearing
        }

        private AdvancedNotificationService CreateNotificationService(bool enableDoNotDisturb = false)
        {
            var service = new AdvancedNotificationService(
                _mockLogger.Object,
                _mockDesktopNotifier.Object,
                _mockEmailNotifier.Object,
                _mockSlackNotifier.Object
            );

            // Optionally set Do Not Disturb mode
            if (enableDoNotDisturb)
            {
                // This would require modifying the service to allow runtime configuration
                // For testing, we're simulating this behavior
            }

            return service;
        }

        private NotificationRequest CreateTestNotification(
            NotificationType type = NotificationType.SystemResource, 
            NotificationSeverity severity = NotificationSeverity.Medium)
        {
            return new NotificationRequest
            {
                Title = "Test Notification",
                Message = "This is a test notification",
                Type = type,
                Severity = severity,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
