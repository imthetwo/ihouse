
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
        var sender = _config["Email:Address"];

        var password = _config["Email:Password"];

        if (string.IsNullOrWhiteSpace(sender))
        {
            throw new InvalidOperationException(
                "Email address is not configured.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Email password is not configured.");
        }

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress("iHouse Management", sender)
        );

        message.To.Add(MailboxAddress.Parse(to));

        message.Subject = subject;

        message.Body = new TextPart("plain")
        {
            Text = body
        };

        using var smtp = new SmtpClient
        {
            Timeout = 15000
        };

        await smtp.ConnectAsync(
            "smtp.gmail.com",
            587,
            SecureSocketOptions.StartTls
        );

        try
        {
            await smtp.AuthenticateAsync(sender, password);

            await smtp.SendAsync(message);
        }
        finally
        {
            if (smtp.IsConnected)
            {
                await smtp.DisconnectAsync(true);
            }
        }
    }
}
