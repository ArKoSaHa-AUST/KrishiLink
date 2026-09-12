using System;
using System.Collections.Generic;
using KrishiLink.Models.Entities;

namespace KrishiLink.Models.ViewModels
{
    public class NotificationItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? TitleKey { get; set; }
        public string? MessageKey { get; set; }
        public string? ArgsJson { get; set; }
        public string LinkUrl { get; set; } = string.Empty;
        public string Type { get; set; } = NotificationTypes.System;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }

        public string TimeAgo => CalculateTimeAgo(CreatedAt);

        public string BadgeColor => Type switch
        {
            NotificationTypes.BookingRequest => "primary",
            NotificationTypes.BookingAccepted => "success",
            NotificationTypes.BookingRejected => "danger",
            NotificationTypes.BookingCompleted => "info",
            NotificationTypes.PayoutProcessed => "success",
            NotificationTypes.PaymentReceived => "success",
            NotificationTypes.ReviewReceived => "warning",
            NotificationTypes.WeatherSuggestion => "warning",
            _ => "secondary"
        };

        public string IconClass => Type switch
        {
            NotificationTypes.BookingRequest => "bi bi-calendar-plus-fill",
            NotificationTypes.BookingAccepted => "bi bi-check-circle-fill",
            NotificationTypes.BookingRejected => "bi bi-x-circle-fill",
            NotificationTypes.BookingCompleted => "bi bi-patch-check-fill",
            NotificationTypes.PayoutProcessed => "bi bi-cash-stack",
            NotificationTypes.PaymentReceived => "bi bi-credit-card-fill",
            NotificationTypes.ReviewReceived => "bi bi-star-fill",
            NotificationTypes.WeatherSuggestion => "bi bi-cloud-sun-fill",
            _ => "bi bi-bell-fill"
        };

        public string BgLightClass => Type switch
        {
            NotificationTypes.BookingRequest => "bg-primary-subtle",
            NotificationTypes.BookingAccepted => "bg-success-subtle",
            NotificationTypes.BookingRejected => "bg-danger-subtle",
            NotificationTypes.BookingCompleted => "bg-info-subtle",
            NotificationTypes.PayoutProcessed => "bg-success-subtle",
            NotificationTypes.PaymentReceived => "bg-success-subtle",
            NotificationTypes.ReviewReceived => "bg-warning-subtle",
            NotificationTypes.WeatherSuggestion => "bg-warning-subtle",
            _ => "bg-light"
        };

        private static string CalculateTimeAgo(DateTime dt)
        {
            var span = DateTime.UtcNow - (dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime());

            if (span.TotalSeconds < 60) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            if (span.TotalDays < 30) return $"{(int)(span.TotalDays / 7)}w ago";
            return dt.ToString("dd MMM yyyy");
        }
    }

    public class PaginatedNotificationsViewModel
    {
        public List<NotificationItemViewModel> Notifications { get; set; } = new();
        public List<NotificationItemViewModel> Items => Notifications;
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public string Filter { get; set; } = "all"; // "all" or "unread"
        public bool UnreadOnly => Filter.Equals("unread", StringComparison.OrdinalIgnoreCase);

        public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 15));
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

    public class NotificationDropdownViewModel
    {
        public int UnreadCount { get; set; }
        public List<NotificationItemViewModel> RecentNotifications { get; set; } = new();
    }
}
