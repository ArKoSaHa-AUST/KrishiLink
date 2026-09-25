using System.Text.RegularExpressions;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    /// <summary>Writes and prunes <see cref="EmailDeliveryLog"/> rows (QLT-03).</summary>
    public interface IEmailDeliveryRecorder
    {
        Task RecordAsync(EmailJob job, EmailDeliveryStatus status, Exception? error, DateTime queuedAt, CancellationToken cancellationToken = default);

        /// <summary>Deletes rows older than <see cref="EmailDeliveryRecorder.RetentionDays"/>; returns how many.</summary>
        Task<int> SweepAsync(CancellationToken cancellationToken = default);
    }

    public sealed partial class EmailDeliveryRecorder : IEmailDeliveryRecorder
    {
        public const int RetentionDays = 180;

        private readonly ApplicationDbContext _db;

        public EmailDeliveryRecorder(ApplicationDbContext db) => _db = db;

        [GeneratedRegex(@"[^\s<>""'(),;:]+@[^\s<>""'(),;:]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
        private static partial Regex EmailAddress();

        /// <summary>A failure reason fit to store: the exception message, capped, with every address fingerprinted.</summary>
        public static string? Describe(Exception? error)
        {
            if (error is null) return null;
            var text = $"{error.GetType().Name}: {error.Message}";
            text = EmailAddress().Replace(text, m => PersonalDataLog.Email(m.Value));
            return text.Length <= 300 ? text : text[..300];
        }

        public async Task RecordAsync(EmailJob job, EmailDeliveryStatus status, Exception? error, DateTime queuedAt, CancellationToken cancellationToken = default)
        {
            _db.EmailDeliveryLogs.Add(new EmailDeliveryLog
            {
                RecipientHash = PersonalDataLog.Email(job.To),
                UserId = job.UserId,
                Subject = job.Subject.Length <= 200 ? job.Subject : job.Subject[..200],
                Status = status,
                Error = Describe(error),
                HadAttachment = job.Attachment is not null,
                QueuedAt = queuedAt,
                AttemptedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        public Task<int> SweepAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            return _db.EmailDeliveryLogs.Where(l => l.AttemptedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        }
    }
}
