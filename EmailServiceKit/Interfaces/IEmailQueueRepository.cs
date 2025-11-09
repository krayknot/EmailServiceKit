using System.Collections.Generic;
using System.Threading.Tasks;

public interface IEmailQueueRepository
{
    Task<long> EnqueueAsync(EmailMessage message);
    Task<IEnumerable<EmailMessage>> GetPendingAsync(int maxCount);
    Task MarkSentAsync(long id);
    Task IncrementAttemptAsync(long id, string? error = null);
    Task InitializeAsync();
}
