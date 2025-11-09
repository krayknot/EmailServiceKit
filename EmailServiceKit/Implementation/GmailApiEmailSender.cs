using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using System;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class GmailApiEmailSender : IEmailSender
{
    private readonly GmailService _service;
    private readonly string _userId;

    // credentials: GoogleCredential created from JSON or token.
    public GmailApiEmailSender(GoogleCredential credential, string userId = "me")
    {
        _service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "EmailServiceKit"
        });
        _userId = userId;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        // build MIME using System.Net.Mail
        var mail = new MailMessage();
        mail.From = new MailAddress(message.From);
        foreach (var to in message.To.Split(',', ';', StringSplitOptions.RemoveEmptyEntries))
            mail.To.Add(to.Trim());
        if (!string.IsNullOrWhiteSpace(message.Cc))
            foreach (var c in message.Cc.Split(',', ';', StringSplitOptions.RemoveEmptyEntries))
                mail.CC.Add(c.Trim());
        if (!string.IsNullOrWhiteSpace(message.Bcc))
            foreach (var b in message.Bcc.Split(',', ';', StringSplitOptions.RemoveEmptyEntries))
                mail.Bcc.Add(b.Trim());

        mail.Subject = message.Subject;
        mail.IsBodyHtml = message.IsHtml;
        mail.Body = message.Body;

        using var mmStream = new System.IO.MemoryStream();
        var client = new SmtpClient(); // use SmtpClient just for building MIME, then export
        // Instead of SmtpClient, construct RFC822 manually:
        var mime = BuildRawMessage(mail);

        var gmailMessage = new Message
        {
            Raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(mime))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=')
        };

        await _service.Users.Messages.Send(gmailMessage, _userId).ExecuteAsync(ct);
    }

    private string BuildRawMessage(MailMessage mail)
    {
        // minimal RFC822 builder
        var sb = new StringBuilder();
        sb.AppendLine($"From: {mail.From}");
        sb.AppendLine($"To: {string.Join(", ", mail.To)}");
        if (mail.CC.Count > 0) sb.AppendLine($"Cc: {string.Join(", ", mail.CC)}");
        sb.AppendLine($"Subject: {mail.Subject}");
        sb.AppendLine("MIME-Version: 1.0");
        sb.AppendLine($"Content-Type: {(mail.IsBodyHtml ? "text/html" : "text/plain")}; charset=UTF-8");
        sb.AppendLine();
        sb.AppendLine(mail.Body);
        return sb.ToString();
    }
}
