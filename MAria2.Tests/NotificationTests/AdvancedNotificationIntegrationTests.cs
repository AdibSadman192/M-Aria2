using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using MAria2.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MAria2.Tests.NotificationTests
{
    public class AdvancedNotificationIntegrationTests : IAsyncLifetime
    {
        private readonly AdvancedNotificationService _notificationService;
        private readonly ConcurrentBag<Notification> _receivedNotifications;
        private readonly Mock<ILogger<AdvancedNotificationService>> _mockLogger;

        // Mock providers
        private readonly Mock<IDesktopNotificationProvider> _mockDesktopProvider;
        private readonly Mock<IEmailNotificationProvider> _mockEmailProvider;
        private readonly Mock<ISlackNotificationProvider> _mockSlackProvider;
        private readonly Mock<ITeamsNotificationProvider> _mockTeamsProvider;

        public AdvancedNotificationIntegrationTests()
        {
            _receivedNotifications = new ConcurrentBag<Notification>();
            _mockLogger = new Mock<ILogger<AdvancedNotificationService>>();

            // Setup mock providers
            _mockDesktopProvider = CreateMockNotificationProvider<IDesktopNotificationProvider>();
            _mockEmailProvider = CreateMockNotificationProvider<IEmailNotificationProvider>();
            _mockSlackProvider = CreateMockNotificationProvider<ISlackNotificationProvider>();
            _mockTeamsProvider = CreateMockNotificationProvider<ITeamsNotificationProvider>();

            // Create notification service
            _notificationService = new AdvancedNotificationService(
                _mockLogger.Object,
                _mockDesktopProvider.Object,
                _mockEmailProvider.Object,
                _mockSlackProvider.Object,
                _mockTeamsProvider.Object
            );
        }

        private Mock<T> CreateMockNotificationProvider<T>() where T : class, INotificationProvider
        {
            var mockProvider = new Mock<T>();
            mockProvider
                .Setup(p => p.SendNotificationAsync(It.IsAny<Notification>()))
                .Callback<Notification>(notification => 
                {
                    _receivedNotifications.Add(notification);
                })
                .Returns(Task.CompletedTask);
            return mockProvider;
        }

        [Fact]
        public async Task SendNotification_MultiProviderScenario_Success()
        {
            // Arrange
            var notification = CreateTestNotification(
                NotificationType.SystemResource, 
                NotificationSeverity.High);

            var selectedChannels = new[] 
            { 
                NotificationChannel.Desktop, 
                NotificationChannel.Slack, 
                NotificationChannel.Teams 
            };

            // Act
            await _notificationService.SendNotificationAsync(notification, selectedChannels);

            // Assert
            Assert.Equal(3, _receivedNotifications.Count);
            Assert.All(_receivedNotifications, n => 
            {
                Assert.Equal(notification.Title, n.Title);
                Assert.Equal(notification.Message, n.Message);
                Assert.Equal(notification.Type, n.Type);
                Assert.Equal(notification.Severity, n.Severity);
            });

            _mockDesktopProvider.Verify(p => p.SendNotificationAsync(It.IsAny<Notification>()), Times.Once);
            _mockSlackProvider.Verify(p => p.SendNotificationAsync(It.IsAny<Notification>()), Times.Once);
            _mockTeamsProvider.Verify(p => p.SendNotificationAsync(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public async Task NotificationFiltering_ComplexScenario_Success()
        {
            // Arrange
            var notifications = new[]
            {
                CreateTestNotification(NotificationType.SystemResource, NotificationSeverity.Critical),
                CreateTestNotification(NotificationType.DownloadProgress, NotificationSeverity.Low),
                CreateTestNotification(NotificationType.Error, NotificationSeverity.High),
                CreateTestNotification(NotificationType.Performance, NotificationSeverity.Medium)
            };

            // Send notifications
            foreach (var notification in notifications)
            {
                await _notificationService.SendNotificationAsync(notification);
            }

            // Complex filter
            var filter = new NotificationFilter
            {
                Types = new[] { NotificationType.SystemResource, NotificationType.Error },
                Severities = new[] { NotificationSeverity.Critical, NotificationSeverity.High }
            };

            // Act
            var filteredNotifications = await _notificationService
                .SubscribeToNotificationsAsync(filter)
                .Take(2)
                .ToListAsync();

            // Assert
            Assert.Equal(2, filteredNotifications.Count);
            Assert.All(filteredNotifications, n => 
            {
                Assert.Contains(n.Type, filter.Types);
                Assert.Contains(n.Severity, filter.Severities);
            });
        }

        [Fact]
        public async Task NotificationPerformance_HighVolume_Scenario()
        {
            // Arrange
            const int notificationCount = 1000;
            var notifications = Enumerable
                .Range(0, notificationCount)
                .Select(_ => CreateTestNotification())
                .ToList();

            // Act
            var tasks = notifications.Select(n => 
                _notificationService.SendNotificationAsync(n));

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(notificationCount, _receivedNotifications.Count);
        }

        [Fact]
        public async Task NotificationErrorHandling_PartialFailure_Scenario()
        {
            // Arrange
            // Simulate a failure in one provider
            _mockSlackProvider
                .Setup(p => p.SendNotificationAsync(It.IsAny<Notification>()))
                .ThrowsAsync(new Exception("Slack connection failed"));

            var notification = CreateTestNotification();
            var selectedChannels = new[] 
            { 
                NotificationChannel.Desktop, 
                NotificationChannel.Slack, 
                NotificationChannel.Teams 
            };

            // Act & Assert
            await _notificationService.SendNotificationAsync(notification, selectedChannels);

            // Verify other providers still work
            _mockDesktopProvider.Verify(p => p.SendNotificationAsync(It.IsAny<Notification>()), Times.Once);
            _mockTeamsProvider.Verify(p => p.SendNotificationAsync(It.IsAny<Notification>()), Times.Once);
        }

        private NotificationRequest CreateTestNotification(
            NotificationType type = NotificationType.SystemResource, 
            NotificationSeverity severity = NotificationSeverity.Medium)
        {
            return new NotificationRequest
            {
                Title = $"Test Notification - {type}",
                Message = $"Detailed message for {type} with {severity} severity",
                Type = type,
                Severity = severity,
                Timestamp = DateTime.UtcNow
            };
        }

        // Implement IAsyncLifetime
        public Task InitializeAsync() => Task.CompletedTask;
        public Task DisposeAsync() => Task.CompletedTask;
    }
}
