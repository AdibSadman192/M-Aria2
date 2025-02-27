using System;
using System.Threading.Tasks;
using MAria2.Infrastructure.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MAria2.Tests.NotificationTests
{
    public class NotificationProvidersTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<SlackNotificationProvider>> _mockSlackLogger;
        private readonly Mock<ILogger<EmailNotificationProvider>> _mockEmailLogger;

        public NotificationProvidersTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockSlackLogger = new Mock<ILogger<SlackNotificationProvider>>();
            _mockEmailLogger = new Mock<ILogger<EmailNotificationProvider>>();

            SetupMockConfiguration();
        }

        private void SetupMockConfiguration()
        {
            // Slack Configuration
            _mockConfiguration
                .Setup(c => c.GetSection("NotificationProviders:Slack")["BotToken"])
                .Returns("test-slack-token");
            _mockConfiguration
                .Setup(c => c.GetSection("NotificationProviders:Slack")["DefaultChannel"])
                .Returns("#test-channel");

            // Email Configuration
            _mockConfiguration
                .Setup(c => c.GetSection("NotificationProviders:Email")["SmtpServer"])
                .Returns("smtp.test.com");
            _mockConfiguration
                .Setup(c => c.GetSection("NotificationProviders:Email")["Port"])
                .Returns("587");
            _mockConfiguration
                .Setup(c => c.GetSection("NotificationProviders:Email")["Username"])
                .Returns("testuser");
            _mockConfiguration
                .Setup(c => c.GetSection("NotificationProviders:Email")["Password"])
                .Returns("testpassword");
            _mockConfiguration
                .Setup(c => c.GetSection("NotificationProviders:Email")["FromEmail"])
                .Returns("from@test.com");
            _mockConfiguration
                .Setup(c => c.GetSection("NotificationProviders:Email")["ToEmail"])
                .Returns("to@test.com");
        }

        [Fact]
        public void SlackNotificationProvider_Initialization_Success()
        {
            // Act
            var slackProvider = new SlackNotificationProvider(
                _mockConfiguration.Object, 
                _mockSlackLogger.Object);

            // Assert
            Assert.NotNull(slackProvider);
        }

        [Fact]
        public void EmailNotificationProvider_Initialization_Success()
        {
            // Act
            var emailProvider = new EmailNotificationProvider(
                _mockConfiguration.Object, 
                _mockEmailLogger.Object);

            // Assert
            Assert.NotNull(emailProvider);
        }

        [Fact]
        public void SlackNotificationProvider_MissingToken_ThrowsException()
        {
            // Arrange
            _mockConfiguration
                .Setup(c => c.GetSection("NotificationProviders:Slack")["BotToken"])
                .Returns(string.Empty);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                new SlackNotificationProvider(
                    _mockConfiguration.Object, 
                    _mockSlackLogger.Object));
        }

        [Fact]
        public void EmailNotificationProvider_MissingConfiguration_ThrowsException()
        {
            // Arrange
            _mockConfiguration
                .Setup(c => c.GetSection("NotificationProviders:Email")["SmtpServer"])
                .Returns(string.Empty);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                new EmailNotificationProvider(
                    _mockConfiguration.Object, 
                    _mockEmailLogger.Object));
        }

        [Fact]
        public async Task SlackNotificationProvider_SendNotification_Successful()
        {
            // Arrange
            var slackProvider = new SlackNotificationProvider(
                _mockConfiguration.Object, 
                _mockSlackLogger.Object);

            var notification = new Notification
            {
                Title = "Test Notification",
                Message = "This is a test message",
                Type = NotificationType.SystemResource,
                Severity = NotificationSeverity.Medium
            };

            // Act & Assert
            // Note: This is a mock test. In a real scenario, you'd mock the Slack client
            await slackProvider.SendNotificationAsync(notification);
        }

        [Fact]
        public async Task EmailNotificationProvider_SendNotification_Successful()
        {
            // Arrange
            var emailProvider = new EmailNotificationProvider(
                _mockConfiguration.Object, 
                _mockEmailLogger.Object);

            var notification = new Notification
            {
                Title = "Test Notification",
                Message = "This is a test message",
                Type = NotificationType.DownloadProgress,
                Severity = NotificationSeverity.Low
            };

            // Act & Assert
            // Note: This is a mock test. In a real scenario, you'd mock the SMTP client
            await emailProvider.SendNotificationAsync(notification);
        }
    }
}
