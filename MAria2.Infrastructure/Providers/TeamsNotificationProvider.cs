using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MAria2.Infrastructure.Providers
{
    public class TeamsNotificationProvider : ITeamsNotificationProvider
    {
        private readonly ILogger<TeamsNotificationProvider> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _webhookUrl;

        public TeamsNotificationProvider(
            IConfiguration configuration, 
            ILogger<TeamsNotificationProvider> logger,
            HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;

            // Retrieve Teams webhook configuration
            var teamsConfig = configuration.GetSection("NotificationProviders:Teams");
            _webhookUrl = teamsConfig["WebhookUrl"];

            ValidateConfiguration();
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_webhookUrl))
            {
                throw new InvalidOperationException("Microsoft Teams webhook URL is not configured.");
            }
        }

        public async Task SendNotificationAsync(Notification notification)
        {
            try 
            {
                var teamsMessage = CreateTeamsMessage(notification);
                var jsonMessage = JsonSerializer.Serialize(teamsMessage);
                var content = new StringContent(jsonMessage, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_webhookUrl, content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Teams notification sent for {notification.Type}");
                }
                else
                {
                    _logger.LogError($"Teams notification failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Teams notification error: {ex.Message}");
            }
        }

        private object CreateTeamsMessage(Notification notification)
        {
            return new
            {
                "@type" = "MessageCard",
                "@context" = "http://schema.org/extensions",
                "themeColor" = GetSeverityColor(notification.Severity),
                "summary" = notification.Title,
                "sections" = new[]
                {
                    new 
                    {
                        "activityTitle" = notification.Title,
                        "activitySubtitle" = notification.Message,
                        "facts" = new[]
                        {
                            new { name = "Notification Type", value = notification.Type.ToString() },
                            new { name = "Severity", value = notification.Severity.ToString() },
                            new { name = "Timestamp", value = notification.Timestamp.ToString("g") }
                        },
                        "markdown" = true
                    }
                },
                "potentialAction" = new[]
                {
                    new 
                    {
                        "@type" = "OpenUri",
                        "name" = "View Details",
                        "targets" = new[]
                        {
                            new 
                            { 
                                "os" = "default", 
                                "uri" = "https://maria2.download/notifications" 
                            }
                        }
                    }
                }
            };
        }

        private string GetSeverityColor(NotificationSeverity severity)
        {
            return severity switch
            {
                NotificationSeverity.Critical => "FF0000",
                NotificationSeverity.High => "FF6600",
                NotificationSeverity.Medium => "FFCC00",
                NotificationSeverity.Low => "36A64F",
                _ => "CCCCCC"
            };
        }
    }

    // Interface extension for Teams-specific provider
    public interface ITeamsNotificationProvider : INotificationProvider
    {
    }
}
