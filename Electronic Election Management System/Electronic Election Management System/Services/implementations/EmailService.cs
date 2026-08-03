using System.Net;
using System.Net.Mail;
using Electronic_Election_Management_System.Services.interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Electronic_Election_Management_System.Services.implementations;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        var emailSettings = _configuration.GetSection("EmailSettings");
        
        var host = emailSettings["Host"];
        var portStr = emailSettings["Port"];
        var userName = emailSettings["UserName"];
        var password = emailSettings["Password"];
        var enableSslStr = emailSettings["EnableSsl"];
        var fromEmail = emailSettings["FromEmail"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(fromEmail))
        {
            _logger.LogWarning("Email settings are not properly configured. Skipping email send to {toEmail}.", toEmail);
            return;
        }

        int port = int.TryParse(portStr, out int p) ? p : 587;
        bool enableSsl = bool.TryParse(enableSslStr, out bool ssl) ? ssl : true;

        try
        {
            var client = new SmtpClient(host, port)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(userName, password),
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail),
                Subject = subject,
                Body = message,
                IsBodyHtml = true,
            };
            
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully to {toEmail} with subject {subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {toEmail}", toEmail);
        }
    }
}
