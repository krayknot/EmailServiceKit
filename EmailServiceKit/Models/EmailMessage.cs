using System;

public class EmailMessage
{
    public long Id { get; set; }
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsHtml { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SendAt { get; set; } // for scheduled sending
    public int AttemptCount { get; set; } = 0;
    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public string? LastError { get; set; }
}
