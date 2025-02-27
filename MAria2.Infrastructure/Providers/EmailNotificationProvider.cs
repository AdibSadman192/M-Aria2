using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MAria2.Infrastructure.Providers
{
    public class EmailNotificationProvider : IEmailNotificationProvider
    {
        private readonly ILogger<EmailNotificationProvider> _logger;
        private readonly SmtpClient _smtpClient;
        private readonly EmailConfiguration _emailConfig;

        public EmailNotificationProvider(
            IConfiguration configuration, 
            ILogger<EmailNotificationProvider> logger)
        {
            _logger = logger;

            // Retrieve email configuration
            var emailConfig = configuration.GetSection("NotificationProviders:Email");
            _emailConfig = new EmailConfiguration
            {
                SmtpServer = emailConfig["SmtpServer"],
                Port = int.Parse(emailConfig["Port"] ?? "587"),
                Username = emailConfig["Username"],
                Password = emailConfig["Password"],
                FromEmail = emailConfig["FromEmail"],
                ToEmail = emailConfig["ToEmail"]
            };

            ValidateConfiguration();

            _smtpClient = new SmtpClient(_emailConfig.SmtpServer)
            {
                Port = _emailConfig.Port,
                Credentials = new NetworkCredential(_emailConfig.Username, _emailConfig.Password),
                EnableSsl = true
            };
        }

        private void ValidateConfiguration()
        {
            var requiredFields = new[]
            {
                nameof(_emailConfig.SmtpServer),
                nameof(_emailConfig.Username),
                nameof(_emailConfig.Password),
                nameof(_emailConfig.FromEmail),
                nameof(_emailConfig.ToEmail)
            };

            foreach (var field in requiredFields)
            {
                var value = _emailConfig.GetType().GetProperty(field)?.GetValue(_emailConfig) as string;
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException($"Email configuration missing: {field}");
                }
            }
        }

        public async Task SendNotificationAsync(Notification notification)
        {
            try 
            {
                var mailMessage = CreateEmailMessage(notification);
                await _smtpClient.SendMailAsync(mailMessage);

                _logger.LogInformation($"Email notification sent for {notification.Type}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Email notification error: {ex.Message}");
            }
        }

        private MailMessage CreateEmailMessage(Notification notification)
        {
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailConfig.FromEmail),
                Subject = $"MAria2 Notification: {notification.Title}",
                Body = CreateEmailBody(notification),
                IsBodyHtml = true
            };

            mailMessage.To.Add(_emailConfig.ToEmail);

            return mailMessage;
        }

        private string CreateEmailBody(Notification notification)
        {
            return $@"
                <html>
                <body>
                    <h2>{notification.Title}</h2>
                    <p>{notification.Message}</p>
                    <hr/>
                    <table>
                        <tr>
                            <td><strong>Type:</strong></td>
                            <td>{notification.Type}</td>
                        </tr>
                        <tr>
                            <td><strong>Severity:</strong></td>
                            <td>{notification.Severity}</td>
                        </tr>
                        <tr>
                            <td><strong>Timestamp:</strong></td>
                            <td>{notification.Timestamp}</td>
                        </tr>
                    </table>
                </body>
                </html>";
        }
    }

    internal class EmailConfiguration
    {
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string FromEmail { get; set; }
        public string ToEmail { get; set; }
    }
}
