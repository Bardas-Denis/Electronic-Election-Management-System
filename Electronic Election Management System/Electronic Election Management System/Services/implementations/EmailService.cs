using Electronic_Election_Management_System.Services.interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

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
        var fromEmail = emailSettings["FromEmail"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(fromEmail))
        {
            _logger.LogWarning("Email settings are not properly configured. Skipping email send to {toEmail}.", toEmail);
            return;
        }

        int port = int.TryParse(portStr, out int p) ? p : 587;

        try
        {
            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress("Electronic Election System", fromEmail));
            mimeMessage.To.Add(new MailboxAddress("", toEmail));
            mimeMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = message };
            mimeMessage.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            // Connect using STARTTLS for port 587
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            
            // Authenticate with the correct Brevo credentials
            await client.AuthenticateAsync(userName, password);
            
            await client.SendAsync(mimeMessage);
            await client.DisconnectAsync(true);
            
            _logger.LogInformation("Email sent successfully to {toEmail} with subject {subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {toEmail}", toEmail);
        }
    }
}
