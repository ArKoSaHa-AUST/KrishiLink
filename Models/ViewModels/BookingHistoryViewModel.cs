using System;
using System.Collections.Generic;

namespace KrishiLink.Models.ViewModels
{
    public class BookingTimelineStep
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? DateDisplay { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsCurrent { get; set; }
        public string State { get; set; } = "done"; // "done", "active", "pending", "rejected"
    }

    public class BookingHistoryItemViewModel
    {
        public int Id { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string BookingType { get; set; } = "Equipment"; // "Equipment" or "Godown"
        public string Category { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DateRangeDisplay => $"{StartDate:dd MMM yyyy} – {EndDate:dd MMM yyyy}";
        public int TotalDays => Math.Max(1, (EndDate - StartDate).Days);

        public decimal TotalCost { get; set; }
        public string CostDisplay => $"৳{TotalCost:N0}";
        public string RateDescription { get; set; } = string.Empty;
        public string QuantityDisplay { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending"; // Pending, Accepted, Completed, Rejected
        public string PaymentStatus { get; set; } = "Pending on Service"; // Paid, Pending on Service, Refunded

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = "+880 1712-345678";
        public string FarmerNotes { get; set; } = string.Empty;
        public string? OwnerRemarks { get; set; }

        public string ListingDetailUrl { get; set; } = "#";
        public List<BookingTimelineStep> Timeline { get; set; } = new();
    }

    public class BookingHistoryViewModel
    {
        // Filters & State
        public string ActiveTab { get; set; } = "all"; // "all", "equipment", "godown"
        public string StatusFilter { get; set; } = "all"; // "all", "pending", "accepted", "completed", "rejected"
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string? SearchTerm { get; set; }

        // Results
        public List<BookingHistoryItemViewModel> Bookings { get; set; } = new();

        // Metrics / Counts
        public int TotalAllCount { get; set; }
        public int EquipmentCount { get; set; }
        public int GodownCount { get; set; }
        public int PendingCount { get; set; }
        public int AcceptedCount { get; set; }
        public int CompletedCount { get; set; }
        public int RejectedCount { get; set; }
    }
}
