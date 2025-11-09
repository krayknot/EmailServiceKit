using MailKit.Net.Smtp;
using MimeKit;
using System.Threading;
using System.Threading.Tasks;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    public SmtpEmailSender(SmtpOptions options) => _options = options;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(message.From));
        mime.To.AddRange(InternetAddressList.Parse(message.To));
        if (!string.IsNullOrEmpty(message.Cc)) mime.Cc.AddRange(InternetAddressList.Parse(message.Cc));
        if (!string.IsNullOrEmpty(message.Bcc)) mime.Bcc.AddRange(InternetAddressList.Parse(message.Bcc));
        mime.Subject = message.Subject;

        var body = new TextPart(message.IsHtml ? "html" : "plain") { Text = message.Body };
        mime.Body = body;

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, _options.UseSsl, ct);
        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}

public record SmtpOptions(string Host, int Port, bool UseSsl, string? Username, string? Password);
