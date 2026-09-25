using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using KrishiLink.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KrishiLink.BLL.Services
{
    /// <summary>One queued e-mail. <paramref name="UserId"/> is the account it concerns, recorded in the delivery log (QLT-03).</summary>
    public record EmailJob(string To, string Subject, string HtmlBody, EmailAttachment? Attachment = null, string? UserId = null);

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
        private readonly Channel<(EmailJob Job, DateTime QueuedAt)> _channel =
            Channel.CreateUnbounded<(EmailJob Job, DateTime QueuedAt)>(new UnboundedChannelOptions { SingleReader = true });
        private readonly IServiceProvider _services;
        private readonly ILogger<EmailDispatchService> _logger;

        public EmailDispatchService(IServiceProvider services, ILogger<EmailDispatchService> logger)
        {
            _services = services;
            _logger = logger;
        }

        public ValueTask EnqueueAsync(EmailJob job, CancellationToken ct = default)
        {
            return _channel.Writer.WriteAsync((job, DateTime.UtcNow), ct);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested && await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                while (_channel.Reader.TryRead(out var item))
                {
                    var (job, queuedAt) = item;
                    using var scope = _services.CreateScope();
                    var status = EmailDeliveryStatus.Sent;
                    Exception? failure = null;
                    try
                    {
                        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                        if (sender is LoggingEmailSender) status = EmailDeliveryStatus.NotConfigured;
                        await sender.SendAsync(job.To, job.Subject, job.HtmlBody, job.Attachment, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                    {
                        status = EmailDeliveryStatus.Failed;
                        failure = ex;
                        _logger.LogError(ex, "Failed to dispatch queued email to {To} with subject {Subject}", PersonalDataLog.Email(job.To), job.Subject);
                    }

                    // So "I never got my receipt" can be answered (QLT-03). Recording must never stop the queue.
                    try
                    {
                        var recorder = scope.ServiceProvider.GetService<IEmailDeliveryRecorder>();
                        if (recorder is not null) await recorder.RecordAsync(job, status, failure, queuedAt, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not record delivery of an email to {To}", PersonalDataLog.Email(job.To));
                    }
                }
            }
        }
    }
}
