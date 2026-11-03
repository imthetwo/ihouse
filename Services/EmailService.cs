using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ihouse.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string body)
    {
        var sender = _config["Email:Address"]
            ?? throw new InvalidOperationException("Email address missing.");

        var password = _config["Email:Password"]
            ?? throw new InvalidOperationException("SMTP password missing.");

        var message = new MimeMessage();

        message.From.Add(new MailboxAddress("iHouse", sender));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var smtp = new SmtpClient
        {
            Timeout = 10000
        };

        await smtp.ConnectAsync(
            "smtp.gmail.com",
            587,
            SecureSocketOptions.StartTls
        );

        await smtp.AuthenticateAsync(sender, password);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }
}