using System;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Slack.NetStandard;
using Slack.NetStandard.WebApi.Chat;

namespace MAria2.Infrastructure.Providers
{
    public class SlackNotificationProvider : ISlackNotificationProvider
    {
        private readonly ILogger<SlackNotificationProvider> _logger;
        private readonly SlackWebApiClient _slackClient;
        private readonly string _defaultChannel;

        public SlackNotificationProvider(
            IConfiguration configuration, 
            ILogger<SlackNotificationProvider> logger)
        {
            _logger = logger;

            // Securely retrieve Slack configuration
            var slackConfig = configuration.GetSection("NotificationProviders:Slack");
            var botToken = slackConfig["BotToken"];
            _defaultChannel = slackConfig["DefaultChannel"] ?? "#general";

            if (string.IsNullOrWhiteSpace(botToken))
            {
                throw new InvalidOperationException("Slack Bot Token is not configured.");
            }

            _slackClient = new SlackWebApiClient(botToken);
        }

        public async Task SendNotificationAsync(Notification notification)
        {
            try 
            {
                var slackMessage = CreateSlackMessage(notification);
                var response = await _slackClient.Chat.PostMessage(slackMessage);

                if (!response.Ok)
                {
                    _logger.LogError($"Slack notification failed: {response.Error}");
                }
                else 
                {
                    _logger.LogInformation($"Slack notification sent for {notification.Type}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Slack notification error: {ex.Message}");
            }
        }

        private Message CreateSlackMessage(Notification notification)
        {
            var color = GetSeverityColor(notification.Severity);
            
            return new Message
            {
                Channel = _defaultChannel,
                Attachments = new[]
                {
                    new Attachment
                    {
                        Color = color,
                        Title = notification.Title,
                        Text = notification.Message,
                        Fields = new[]
                        {
                            new AttachmentField 
                            { 
                                Title = "Type", 
                                Value = notification.Type.ToString(), 
                                Short = true 
                            },
                            new AttachmentField 
                            { 
                                Title = "Severity", 
                                Value = notification.Severity.ToString(), 
                                Short = true 
                            }
                        },
                        Timestamp = notification.Timestamp
                    }
                }
            };
        }

        private string GetSeverityColor(NotificationSeverity severity)
        {
            return severity switch
            {
                NotificationSeverity.Critical => "#FF0000",
                NotificationSeverity.High => "#FF6600",
                NotificationSeverity.Medium => "#FFCC00",
                NotificationSeverity.Low => "#36A64F",
                _ => "#CCCCCC"
            };
        }
    }
}
