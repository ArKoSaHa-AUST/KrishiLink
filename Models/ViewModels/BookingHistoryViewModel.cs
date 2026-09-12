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

        public string Status { get; set; } = "Pending"; // Pending, Accepted, Paid, Completed, Rejected, Cancelled
        public string PaymentStatus { get; set; } = "Payment required";

        // Escrow payment (null until the farmer has paid)
        public string? PaymentReference { get; set; }
        public string? PaymentMethod { get; set; }
        public DateTime? PaidOn { get; set; }
        public DateTime? RefundedOn { get; set; }

        /// <summary>True while the booking is Accepted and awaiting the farmer's payment.</summary>
        public bool CanPay { get; set; }
        public string PayUrl { get; set; } = "#";

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public string FarmerNotes { get; set; } = string.Empty;
        public string? OwnerRemarks { get; set; }

        public string ListingDetailUrl { get; set; } = "#";
        public int ListingId { get; set; }
        public bool CanCancel { get; set; }
        public bool CanModify { get; set; }
        public int Units { get; set; } = 1;
        public double StorageTons { get; set; }
        public int MaxUnits { get; set; } = 1;
        public int MinDays { get; set; } = 1;
        public int ModificationCount { get; set; } = 0;
        public string? PreviousDetails { get; set; }
        public int? HarvestPlanId { get; set; }
        public string? HarvestPlanName { get; set; }
        public List<BookingTimelineStep> Timeline { get; set; } = new();

        // Review Information (For Completed bookings)
        public bool HasReview { get; set; }
        public int? ReviewRating { get; set; }
        public string? ReviewComment { get; set; }
        public DateTime? ReviewedAt { get; set; }
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
        public int PaidCount { get; set; }
        public int CompletedCount { get; set; }
        public int RejectedCount { get; set; }
        public int CancelledCount { get; set; }
    }
}
