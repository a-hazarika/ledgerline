using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Ledgerline.Api.Email;

public interface IEmailSender
{
    Task SendAsync(RenderedEmail message, string toAddress, string toName, CancellationToken cancellationToken);
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        RenderedEmail message,
        string toAddress,
        string toName,
        CancellationToken cancellationToken)
    {
        var mime = new MimeMessage
        {
            Subject = message.Subject,
            Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody()
        };

        mime.From.Add(new MailboxAddress(message.FromName, message.FromAddress));
        mime.To.Add(new MailboxAddress(toName, toAddress));

        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            mime.ReplyTo.Add(MailboxAddress.Parse(message.ReplyTo));
        }

        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        _logger.LogInformation("Delivered {Subject} to {Recipient}", message.Subject, toAddress);
    }
}
