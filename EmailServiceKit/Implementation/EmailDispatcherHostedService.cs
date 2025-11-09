using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using System;
using System.Threading;
using System.Threading.Tasks;

public class EmailDispatcherHostedService : BackgroundService
{
    private readonly IEmailQueueRepository _repo;
    private readonly IEmailSender _sender; // primary sender; swap using config or factory
    private readonly ILogger<EmailDispatcherHostedService> _logger;
    private readonly int _batchSize = 10;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);

    public EmailDispatcherHostedService(IEmailQueueRepository repo, IEmailSender sender, ILogger<EmailDispatcherHostedService> logger)
    {
        _repo = repo;
        _sender = sender;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _repo.InitializeAsync();
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var policy = RetryPolicies.DefaultPolicy();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pending = await _repo.GetPendingAsync(_batchSize);
                foreach (var msg in pending)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        await policy.ExecuteAsync(async ct =>
                        {
                            await _sender.SendAsync(msg, ct);
                        }, stoppingToken);

                        await _repo.MarkSentAsync(msg.Id);
                        _logger.LogInformation("Email {Id} sent", msg.Id);
                    }
                    catch (Exception ex)
                    {
                        await _repo.IncrementAttemptAsync(msg.Id, ex.Message);
                        _logger.LogError(ex, "Failed to send email {Id}", msg.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatcher poll failed");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
