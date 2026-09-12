using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KrishiLink.BLL.Services
{
    public record EmailJob(string To, string Subject, string HtmlBody, EmailAttachment? Attachment = null);

    public interface IEmailQueue
    {
        ValueTask EnqueueAsync(EmailJob job, CancellationToken ct = default);
    }

    /// <summary>
    /// Background channel consumer for non-blocking asynchronous email dispatch.
    /// Creates a dedicated scope per message to safely resolve scoped IEmailSender.
    /// </summary>
    public class EmailDispatchService : BackgroundService, IEmailQueue
    {
        private readonly Channel<EmailJob> _channel = Channel.CreateUnbounded<EmailJob>(new UnboundedChannelOptions { SingleReader = true });
        private readonly IServiceProvider _services;
        private readonly ILogger<EmailDispatchService> _logger;

        public EmailDispatchService(IServiceProvider services, ILogger<EmailDispatchService> logger)
        {
            _services = services;
            _logger = logger;
        }

        public ValueTask EnqueueAsync(EmailJob job, CancellationToken ct = default)
        {
            return _channel.Writer.WriteAsync(job, ct);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested && await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                while (_channel.Reader.TryRead(out var job))
                {
                    try
                    {
                        using var scope = _services.CreateScope();
                        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                        await sender.SendAsync(job.To, job.Subject, job.HtmlBody, job.Attachment, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to dispatch queued email to {To} with subject {Subject}", job.To, job.Subject);
                    }
                }
            }
        }
    }
}
