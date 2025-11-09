# 📘 EmailServiceKit – Complete Usage Guide

## 1. Overview

`EmailServiceKit` is a **lightweight, modular email infrastructure layer** for .NET.
It unifies **Gmail API** and **SMTP** sending under one abstraction, adds **SQLite-based queuing**, **retry logic**, **template rendering**, and **scheduled sending**.

It is built for production workloads like:

* Background job processors (e.g., sending newsletters)
* Web APIs (contact forms, signup verification)
* Desktop or MAUI apps needing queued/scheduled mail
* Serverless or self-hosted services

---

## 2. Installation

You can clone or copy `/src/EmailServiceKit` into your solution.
Alternatively, add it as a library reference:

```bash
dotnet add reference ../EmailServiceKit/EmailServiceKit.csproj
```

Or build a package and reuse:

```bash
dotnet pack src/EmailServiceKit/EmailServiceKit.csproj -o ./nupkgs
dotnet add package EmailServiceKit --source ./nupkgs
```

Then restore packages:

```bash
dotnet restore
```

---

## 3. Configuration

The library uses `appsettings.json` for configuration.
Example:

```json
{
  "Email": {
    "Sqlite": {
      "ConnectionString": "Data Source=emailqueue.db"
    },
    "Smtp": {
      "Host": "smtp.mailtrap.io",
      "Port": 587,
      "UseSsl": true,
      "Username": "your_username",
      "Password": "your_password"
    },
    "Gmail": {
      "Enabled": false,
      "CredentialJson": ""
    }
  }
}
```

> If `"Gmail.Enabled": true`, the kit uses Gmail API with a JSON credential string or file.

---

## 4. Registering the service (Dependency Injection)

In your `Program.cs` or `Startup.cs` (ASP.NET Core or Worker Service):

```csharp
var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: false);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddEmailServiceKit(ctx.Configuration);
        services.AddHostedService<YourAppBackgroundJob>(); // optional
    });

await builder.RunConsoleAsync();
```

The extension `AddEmailServiceKit()` automatically:

* Sets up the SQLite queue repository.
* Registers `IEmailSender` (SMTP or Gmail depending on config).
* Adds Razor-based template renderer.
* Starts the background dispatcher service.

---

## 5. Enqueuing emails

Instead of sending directly, you enqueue them.
They’re stored in a SQLite queue table and processed by the background worker.

```csharp
var queue = serviceProvider.GetRequiredService<IEmailQueueRepository>();

await queue.EnqueueAsync(new EmailMessage
{
    From = "no-reply@yourapp.com",
    To = "user@example.com",
    Subject = "Welcome to our platform!",
    Body = "<h2>Hello!</h2><p>Thanks for joining us.</p>",
    IsHtml = true,
    SendAt = DateTimeOffset.UtcNow // or a future time for scheduling
});
```

The dispatcher automatically detects pending emails, applies retry logic, and sends them via the configured sender.

---

## 6. Sending directly (bypassing queue)

If you need to send an immediate email (e.g., user verification):

```csharp
var sender = serviceProvider.GetRequiredService<IEmailSender>();

await sender.SendAsync(new EmailMessage
{
    From = "no-reply@domain.com",
    To = "customer@domain.com",
    Subject = "Instant Notification",
    Body = "This was sent directly.",
    IsHtml = false
});
```

> Use direct sending sparingly — queued delivery ensures retries and resiliency.

---

## 7. Template rendering

The built-in `RazorTemplateRenderer` lets you use C# Razor templates for dynamic content.

```csharp
var renderer = serviceProvider.GetRequiredService<ITemplateRenderer>();
var html = await renderer.RenderAsync(
    "WelcomeTemplate",
    new { Name = "Alex", Plan = "Premium" }
);

await queue.EnqueueAsync(new EmailMessage
{
    From = "no-reply@domain.com",
    To = "alex@domain.com",
    Subject = "Welcome!",
    Body = html,
    IsHtml = true
});
```

You can store templates as `.cshtml` files, database strings, or embedded resources.

---

## 8. Scheduled sending

Any message with a future `SendAt` timestamp will be delayed until the time arrives.

```csharp
await queue.EnqueueAsync(new EmailMessage
{
    From = "me@domain.com",
    To = "subscriber@domain.com",
    Subject = "Tomorrow’s Newsletter",
    Body = "<p>Daily digest...</p>",
    SendAt = DateTimeOffset.UtcNow.AddDays(1)
});
```

The background worker checks every 10 seconds (default) and sends eligible emails.

---

## 9. Retry and error handling

Failed emails are retried automatically using **Polly retry policies**:

* 1st retry after 2s
* 2nd retry after 5s
* 3rd retry after 10s

If still failing, the email’s `Status` becomes `Failed` and its `LastError` column stores the exception message.

You can query failed records for reporting or requeue them manually.

---

## 10. Logging

`EmailDispatcherHostedService` uses the default .NET logging provider.
Add a console logger in your host builder for diagnostics:

```csharp
.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});
```

You’ll see logs like:

```
info: EmailDispatcherHostedService[0]
      Email 12 sent successfully
error: EmailDispatcherHostedService[0]
      Failed to send email 13: Timeout
```

---

## 11. Integration in ASP.NET Core

You can easily expose endpoints for email queueing:

```csharp
[ApiController]
[Route("api/email")]
public class EmailController : ControllerBase
{
    private readonly IEmailQueueRepository _queue;
    public EmailController(IEmailQueueRepository queue) => _queue = queue;

    [HttpPost("send")]
    public async Task<IActionResult> Enqueue([FromBody] EmailMessage msg)
    {
        var id = await _queue.EnqueueAsync(msg);
        return Ok(new { id, status = "queued" });
    }
}
```

This allows your web app or admin panel to queue emails dynamically.

---

## 12. Switching between SMTP and Gmail API

The system automatically chooses based on configuration:

* **SMTP mode** (default): uses MailKit, standard host/port credentials.
* **Gmail mode**: uses OAuth2 credentials for sending through Gmail REST API.

Toggle it via `appsettings.json`:

```json
"Email": {
  "Gmail": { "Enabled": true, "CredentialJson": "{...}" }
}
```

---

## 13. Background dispatcher overview

The dispatcher (`EmailDispatcherHostedService`) runs automatically when the host starts.

Responsibilities:

1. Poll the queue table every 10 seconds.
2. Fetch up to 10 pending emails (`Status = Pending`).
3. Try sending each one with retry policy.
4. Mark as `Sent` or `Failed` in the database.

This runs indefinitely, managing scheduling and retries.

---

## 14. Database schema

Created automatically (via `InitializeAsync()`):

```sql
CREATE TABLE IF NOT EXISTS EmailQueue (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  FromAddress TEXT NOT NULL,
  ToAddress TEXT NOT NULL,
  Cc TEXT,
  Bcc TEXT,
  Subject TEXT,
  Body TEXT,
  IsHtml INTEGER,
  CreatedAt TEXT,
  SendAt TEXT,
  AttemptCount INTEGER DEFAULT 0,
  Status INTEGER DEFAULT 0,
  LastError TEXT
);
```

You can inspect queue state anytime using SQLite tools.

---

## 15. Example: sending with Razor template + scheduling

```csharp
var renderer = provider.GetRequiredService<ITemplateRenderer>();
var html = await renderer.RenderAsync(
    "Welcome",
    new { Name = "Chris", Discount = "20%" }
);

var msg = new EmailMessage
{
    From = "sales@domain.com",
    To = "chris@domain.com",
    Subject = "Special Offer!",
    Body = html,
    IsHtml = true,
    SendAt = DateTimeOffset.UtcNow.AddMinutes(5)
};

await provider.GetRequiredService<IEmailQueueRepository>().EnqueueAsync(msg);
```

This schedules a templated email for 5 minutes later.

---

## 16. Advanced options

* **Retry customization:** Modify `RetryPolicies.DefaultPolicy()` for different retry intervals or exception filters.
* **Template source:** Replace `RazorTemplateRenderer` with a database-based one.
* **Queue database:** Replace `SqliteEmailQueueRepository` with MySQL or PostgreSQL implementation.
* **Dispatcher frequency:** Edit `_pollInterval` in `EmailDispatcherHostedService`.
* **Batch size:** Increase `_batchSize` for bulk sending.

---

## 17. Ideal use cases

✅ Transactional emails (signups, OTPs, receipts)
✅ Newsletters with scheduled delivery
✅ Multi-tenant systems needing per-domain sender setup
✅ Offline-capable desktop/Mobile apps
✅ Low-overhead alternative to large email infrastructure

---

## 18. Summary

**EmailServiceKit** gives your .NET project a plug-and-play, fault-tolerant, and extensible email delivery system.
You can send via **SMTP** or **Gmail API**, queue emails safely, retry failed ones automatically, and render personalized templates — all without introducing complex dependencies.

It’s production-ready for:

* ASP.NET Core Web APIs
* Background worker services
* Desktop/Mobile apps
* Self-hosted microservices
