using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    /// <summary>
    /// Bound from the "Email" section. The SMTP password is deliberately not in appsettings.json —
    /// supply it via user-secrets or the Email__Password environment variable.
    /// </summary>
    public class EmailOptions
    {
        public const string SectionName = "Email";

        public bool Enabled { get; set; }
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "KrishiLink";

        public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
    }

    public record EmailAttachment(string FileName, byte[] Content, string ContentType);

    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string htmlBody, EmailAttachment? attachment = null, CancellationToken ct = default);
    }

    /// <summary>Plain SMTP sender using the framework client; adequate for transactional mail such as statements.</summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;

        public SmtpEmailSender(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendAsync(string to, string subject, string htmlBody, EmailAttachment? attachment = null, CancellationToken ct = default)
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);
            if (attachment is not null)
                message.Attachments.Add(new Attachment(new MemoryStream(attachment.Content), attachment.FileName, attachment.ContentType));

            using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.UseSsl };
            if (!string.IsNullOrEmpty(_options.Username))
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);

            ct.ThrowIfCancellationRequested();
            await client.SendMailAsync(message, ct);
        }
    }

    /// <summary>Used when email is not configured: records what would have been sent so the flow can still be exercised locally.</summary>
    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(string to, string subject, string htmlBody, EmailAttachment? attachment = null, CancellationToken ct = default)
        {
            _logger.LogInformation("Email not configured — would send to {To}: \"{Subject}\"{Attachment}", to, subject,
                attachment is null ? string.Empty : $" with {attachment.FileName} ({attachment.Content.Length:N0} bytes)");
            return Task.CompletedTask;
        }
    }
}
