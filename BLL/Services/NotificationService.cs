using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;

namespace KrishiLink.BLL.Services
{
    public interface INotificationService
    {
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
        private readonly IEmailSender _emailSender;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IRepository<Notification> notifications,
            IEmailSender emailSender,
            ILogger<NotificationService> logger)
        {
            _notifications = notifications;
            _emailSender = emailSender;
            _logger = logger;
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
            var notification = new Notification
            {
                UserId = userId,
                Title = title.Trim(),
                Message = message.Trim(),
                LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? "/" : linkUrl.Trim(),
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _notifications.AddAsync(notification);
            await _notifications.SaveChangesAsync();

            if (sendEmail && !string.IsNullOrWhiteSpace(recipientEmail))
            {
                _ = SendEmailNotificationAsync(recipientEmail, title, message, linkUrl);
            }

            return notification;
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
                .Select(n => new NotificationItemViewModel
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    LinkUrl = n.LinkUrl,
                    Type = n.Type,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return list;
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
                .Select(n => new NotificationItemViewModel
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    LinkUrl = n.LinkUrl,
                    Type = n.Type,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return new PaginatedNotificationsViewModel
            {
                Notifications = items,
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
            try
            {
                var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background-color: #f3f4f6; margin: 0; padding: 24px;'>
    <div style='max-width: 580px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.05); border: 1px solid #e5e7eb;'>
        <div style='background-color: #16a34a; padding: 20px 24px; color: #ffffff;'>
            <h2 style='margin: 0; font-size: 20px; font-weight: 700; display: flex; align-items: center;'>🌾 KrishiLink Notification</h2>
        </div>
        <div style='padding: 24px;'>
            {htmlMessage}
        </div>
        <div style='background-color: #f9fafb; padding: 16px 24px; border-top: 1px solid #f3f4f6; font-size: 12px; color: #9ca3af; text-align: center;'>
            © {DateTime.UtcNow.Year} KrishiLink — Empowering Farmers & Agricultural Providers in Bangladesh.<br/>
            You received this notification because of an activity on your account.
        </div>
    </div>
</body>
</html>";

                await _emailSender.SendAsync(email, subject.StartsWith("[KrishiLink]") ? subject : $"[KrishiLink] {subject}", htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send transactional email to {Email}", email);
            }
        }

        private Task SendEmailNotificationAsync(string email, string title, string message, string linkUrl)
        {
            var content = $@"
                <h3 style='color: #111827; margin-top: 0; font-size: 18px;'>{title}</h3>
                <p style='color: #4b5563; font-size: 15px; line-height: 1.6;'>{message}</p>
                <div style='margin-top: 28px; text-align: center;'>
                    <a href='https://krishilink.com{linkUrl}' style='background-color: #16a34a; color: #ffffff; text-decoration: none; padding: 12px 28px; border-radius: 30px; font-weight: 600; font-size: 14px; display: inline-block;'>View in KrishiLink</a>
                </div>";
            return SendEmailNotificationAsync(email, title, content);
        }
    }
}
