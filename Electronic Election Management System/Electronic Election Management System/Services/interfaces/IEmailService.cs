namespace Electronic_Election_Management_System.Services.interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string message);
}
