using System.Threading;
using System.Threading.Tasks;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
