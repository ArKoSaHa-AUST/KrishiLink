using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KrishiLink.Models.Entities
{
    public static class NotificationTypes
    {
        public const string BookingRequest = "BookingRequest";
        public const string BookingAccepted = "BookingAccepted";
        public const string BookingRejected = "BookingRejected";
        public const string BookingCompleted = "BookingCompleted";
        public const string PayoutProcessed = "PayoutProcessed";
        public const string ReviewReceived = "ReviewReceived";
        public const string WeatherSuggestion = "WeatherSuggestion";
        public const string System = "System";
    }

    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        [StringLength(255)]
        public string LinkUrl { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = NotificationTypes.System;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
