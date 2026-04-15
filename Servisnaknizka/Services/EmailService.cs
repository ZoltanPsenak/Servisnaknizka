using System.Net;
using System.Net.Mail;

namespace Servisnaknizka.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var smtpHost = _configuration["Email:SmtpHost"] ?? "mx1.lznet.work";
        var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var smtpUser = _configuration["Email:SmtpUser"] ?? "";
        var smtpPass = _configuration["Email:SmtpPass"] ?? "";
        var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@lznet.work";
        var fromName = _configuration["Email:FromName"] ?? "Servisná Knižka";

        using var message = new MailMessage();
        message.From = new MailAddress(fromEmail, fromName);
        message.To.Add(new MailAddress(toEmail));
        message.Subject = subject;
        message.Body = htmlBody;
        message.IsBodyHtml = true;

        using var client = new SmtpClient(smtpHost, smtpPort);
        client.EnableSsl = true;
        if (!string.IsNullOrEmpty(smtpUser))
        {
            client.Credentials = new NetworkCredential(smtpUser, smtpPass);
        }

        try
        {
            await client.SendMailAsync(message);
            _logger.LogInformation("Email odoslaný na {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chyba pri odosielaní emailu na {Email}", toEmail);
            throw;
        }
    }
}
