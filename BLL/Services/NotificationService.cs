using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    public class NotificationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = NotificationTypes.System;
        public string TitleKey { get; set; } = string.Empty;
        public string MessageKey { get; set; } = string.Empty;
        public object[] Args { get; set; } = Array.Empty<object>();
        public string? LinkUrl { get; set; }
        public string? DedupeKey { get; set; }
        public bool SendEmail { get; set; }
        public string? RecipientEmail { get; set; }
    }

    public interface INotificationService
    {
        Task<bool> NotifyAsync(NotificationRequest request);
        Task<Notification> CreateNotificationAsync(string userId, string title, string message, string linkUrl, string type = NotificationTypes.System, bool sendEmail = false, string? recipientEmail = null);
        Task<Notification> CreateAsync(string userId, string type, string title, string message, string? linkUrl = null, bool sendEmail = false, string? recipientEmail = null);
        Task<int> GetUnreadCountAsync(string userId);
        Task<List<NotificationItemViewModel>> GetRecentNotificationsAsync(string userId, int count = 8);
        Task<PaginatedNotificationsViewModel> GetUserNotificationsAsync(string userId, int page = 1, int pageSize = 15, string filter = "all");
        Task<PaginatedNotificationsViewModel> GetPaginatedNotificationsAsync(string userId, bool? unreadOnly = null, int page = 1, int pageSize = 15);
        Task<bool> MarkAsReadAsync(int notificationId, string userId);
        Task<int> MarkAllAsReadAsync(string userId);
        Task<bool> DeleteNotificationAsync(int notificationId, string userId);
        Task<bool> DeleteAsync(int notificationId, string userId);
        Task<Notification?> GetNotificationAsync(int notificationId, string userId);
        Task SendEmailNotificationAsync(string email, string subject, string htmlMessage);
    }

    public class NotificationService : INotificationService
    {
        private readonly IRepository<Notification> _notifications;
        private readonly IEmailQueue _emailQueue;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly AppOptions _appOptions;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IRepository<Notification> notifications,
            IEmailQueue emailQueue,
            IStringLocalizer<SharedResource> localizer,
            IOptions<AppOptions> appOptions,
            ILogger<NotificationService> logger)
        {
            _notifications = notifications;
            _emailQueue = emailQueue;
            _localizer = localizer;
            _appOptions = appOptions.Value;
            _logger = logger;
        }

        public async Task<bool> NotifyAsync(NotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return false;

            var stringArgs = request.Args.Select(a => a?.ToString() ?? string.Empty).ToArray();
            var argsJson = stringArgs.Length > 0 ? JsonSerializer.Serialize(stringArgs) : null;

            // Render default English/current-culture text for fallback/storage
            var resolvedTitle = ResolveString(request.TitleKey, stringArgs);
            var resolvedMessage = ResolveString(request.MessageKey, stringArgs);

            var notification = new Notification
            {
                UserId = request.UserId,
                Type = request.Type,
                TitleKey = request.TitleKey,
                MessageKey = request.MessageKey,
                ArgsJson = argsJson,
                Title = resolvedTitle,
                Message = resolvedMessage,
                LinkUrl = string.IsNullOrWhiteSpace(request.LinkUrl) ? "/" : request.LinkUrl.Trim(),
                DedupeKey = string.IsNullOrWhiteSpace(request.DedupeKey) ? null : request.DedupeKey.Trim(),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _notifications.AddAsync(notification);
                await _notifications.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                _logger.LogInformation("Notification with DedupeKey {DedupeKey} for user {UserId} already exists. Skipping duplicate.", request.DedupeKey, request.UserId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save notification for user {UserId}", request.UserId);
                return false;
            }

            if (request.SendEmail && ApplicationUser.HasRealEmail(request.RecipientEmail))
            {
                var fullLink = BuildAbsoluteUrl(notification.LinkUrl);
                var emailBody = BuildEmailHtml(resolvedTitle, resolvedMessage, fullLink);
                var emailJob = new EmailJob(request.RecipientEmail!.Trim(), $"[KrishiLink] {resolvedTitle}", emailBody);
                await _emailQueue.EnqueueAsync(emailJob);
            }

            return true;
        }

        public async Task<Notification> CreateNotificationAsync(
            string userId,
            string title,
            string message,
            string linkUrl,
            string type = NotificationTypes.System,
            bool sendEmail = false,
            string? recipientEmail = null)
        {
            var req = new NotificationRequest
            {
                UserId = userId,
                Type = type,
                TitleKey = title,
                MessageKey = message,
                LinkUrl = linkUrl,
                SendEmail = sendEmail,
                RecipientEmail = recipientEmail
            };

            await NotifyAsync(req);

            return new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                LinkUrl = linkUrl,
                Type = type,
                CreatedAt = DateTime.UtcNow
            };
        }

        public Task<Notification> CreateAsync(
            string userId,
            string type,
            string title,
            string message,
            string? linkUrl = null,
            bool sendEmail = false,
            string? recipientEmail = null)
        {
            return CreateNotificationAsync(userId, title, message, linkUrl ?? "/", type, sendEmail, recipientEmail);
        }

        public Task<int> GetUnreadCountAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return Task.FromResult(0);
            return _notifications.Query().CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<List<NotificationItemViewModel>> GetRecentNotificationsAsync(string userId, int count = 8)
        {
            if (string.IsNullOrWhiteSpace(userId)) return new List<NotificationItemViewModel>();

            var list = await _notifications.Query()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync();

            return list.Select(ToViewModel).ToList();
        }

        public async Task<PaginatedNotificationsViewModel> GetUserNotificationsAsync(string userId, int page = 1, int pageSize = 15, string filter = "all")
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;

            var baseQuery = _notifications.Query().Where(n => n.UserId == userId);
            var unreadCount = await baseQuery.CountAsync(n => !n.IsRead);

            var query = filter.Equals("unread", StringComparison.OrdinalIgnoreCase)
                ? baseQuery.Where(n => !n.IsRead)
                : baseQuery;

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedNotificationsViewModel
            {
                Notifications = items.Select(ToViewModel).ToList(),
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                UnreadCount = unreadCount,
                Filter = filter.ToLowerInvariant()
            };
        }

        public Task<PaginatedNotificationsViewModel> GetPaginatedNotificationsAsync(string userId, bool? unreadOnly = null, int page = 1, int pageSize = 15)
        {
            var filter = unreadOnly == true ? "unread" : "all";
            return GetUserNotificationsAsync(userId, page, pageSize, filter);
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, string userId)
        {
            var notification = await _notifications.QueryTracked()
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification is null) return false;

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _notifications.SaveChangesAsync();
            }

            return true;
        }

        public async Task<int> MarkAllAsReadAsync(string userId)
        {
            var unread = await _notifications.QueryTracked()
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unread.Count == 0) return 0;

            foreach (var item in unread)
            {
                item.IsRead = true;
            }

            await _notifications.SaveChangesAsync();
            return unread.Count;
        }

        public async Task<bool> DeleteNotificationAsync(int notificationId, string userId)
        {
            var notification = await _notifications.QueryTracked()
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification is null) return false;

            _notifications.Remove(notification);
            await _notifications.SaveChangesAsync();
            return true;
        }

        public Task<bool> DeleteAsync(int notificationId, string userId)
        {
            return DeleteNotificationAsync(notificationId, userId);
        }

        public Task<Notification?> GetNotificationAsync(int notificationId, string userId)
        {
            return _notifications.QueryTracked()
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        }

        public async Task SendEmailNotificationAsync(string email, string subject, string htmlMessage)
        {
            if (!ApplicationUser.HasRealEmail(email)) return;

            var job = new EmailJob(email.Trim(), subject.StartsWith("[KrishiLink]") ? subject : $"[KrishiLink] {subject}", htmlMessage);
            await _emailQueue.EnqueueAsync(job);
        }

        private NotificationItemViewModel ToViewModel(Notification n)
        {
            string[] args = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(n.ArgsJson))
            {
                try
                {
                    var deserialized = JsonSerializer.Deserialize<string[]>(n.ArgsJson);
                    if (deserialized != null) args = deserialized;
                }
                catch { }
            }

            var title = !string.IsNullOrWhiteSpace(n.TitleKey)
                ? ResolveString(n.TitleKey, args)
                : n.Title;

            var message = !string.IsNullOrWhiteSpace(n.MessageKey)
                ? ResolveString(n.MessageKey, args)
                : n.Message;

            return new NotificationItemViewModel
            {
                Id = n.Id,
                Title = title,
                Message = message,
                TitleKey = n.TitleKey,
                MessageKey = n.MessageKey,
                ArgsJson = n.ArgsJson,
                LinkUrl = n.LinkUrl,
                Type = n.Type,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            };
        }

        private string ResolveString(string key, string[] args)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            try
            {
                var localized = args.Length > 0 ? _localizer[key, args] : _localizer[key];
                if (!localized.ResourceNotFound && !string.IsNullOrWhiteSpace(localized.Value))
                {
                    return localized.Value;
                }
            }
            catch { }

            if (args.Length > 0)
            {
                try
                {
                    return string.Format(key, args);
                }
                catch { }
            }

            return key;
        }

        private string BuildAbsoluteUrl(string relativeUrl)
        {
            var baseUrl = (_appOptions.PublicBaseUrl ?? "https://localhost:7276").TrimEnd('/');
            var rel = (relativeUrl ?? "/").TrimStart('~');
            if (!rel.StartsWith("/")) rel = "/" + rel;
            return $"{baseUrl}{rel}";
        }

        private string BuildEmailHtml(string title, string message, string fullLink)
        {
            return $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background-color: #f3f4f6; margin: 0; padding: 24px;'>
    <div style='max-width: 580px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.05); border: 1px solid #e5e7eb;'>
        <div style='background-color: #16a34a; padding: 20px 24px; color: #ffffff;'>
            <h2 style='margin: 0; font-size: 20px; font-weight: 700;'>🌾 KrishiLink Notification</h2>
        </div>
        <div style='padding: 24px;'>
            <h3 style='color: #111827; margin-top: 0; font-size: 18px;'>{title}</h3>
            <p style='color: #4b5563; font-size: 15px; line-height: 1.6;'>{message}</p>
            <div style='margin-top: 28px; text-align: center;'>
                <a href='{fullLink}' style='background-color: #16a34a; color: #ffffff; text-decoration: none; padding: 12px 28px; border-radius: 30px; font-weight: 600; font-size: 14px; display: inline-block;'>View in KrishiLink</a>
            </div>
        </div>
        <div style='background-color: #f9fafb; padding: 16px 24px; border-top: 1px solid #f3f4f6; font-size: 12px; color: #9ca3af; text-align: center;'>
            © {DateTime.UtcNow.Year} KrishiLink — Empowering Farmers & Agricultural Providers in Bangladesh.<br/>
            You received this notification because of activity on your account.
        </div>
    </div>
</body>
</html>";
        }
    }
}
