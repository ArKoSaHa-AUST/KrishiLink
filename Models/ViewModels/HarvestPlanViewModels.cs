using System;
using System.Collections.Generic;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;

namespace KrishiLink.Models.ViewModels
{
    public class HarvestPlanIndexViewModel
    {
        public List<HarvestPlanListViewModel> Plans { get; set; } = new();
        public int DraftCount => Plans.Count(p => p.Status == HarvestPlanStatus.Draft);
        public bool CanCreatePlan => DraftCount < 5;
    }

    public class HarvestPlanListViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Crop { get; set; }
        public string? Note { get; set; }
        public string Status { get; set; } = HarvestPlanStatus.Draft;
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedOn { get; set; }

        public int ItemCount { get; set; }
        public decimal TotalEstimatedGross { get; set; }
        public string DateRangeSummary { get; set; } = string.Empty;

        public int PendingCount { get; set; }
        public int ConfirmedCount { get; set; }
        public int CompletedCount { get; set; }

        public bool CanSubmit => Status == HarvestPlanStatus.Draft && ItemCount > 0;
        public bool CanClone => ItemCount > 0;
        public bool CanDelete => Status != HarvestPlanStatus.Submitted;
        public bool IsSubmitted => Status == HarvestPlanStatus.Submitted;
        public bool IsClosed => Status == HarvestPlanStatus.Closed;
    }

    public class HarvestPlanDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Crop { get; set; }
        public string? Note { get; set; }
        public string Status { get; set; } = HarvestPlanStatus.Draft;
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedOn { get; set; }

        public decimal TotalEstimatedGross { get; set; }
        public bool HasConflicts { get; set; }
        public string TargetDateRange => Items.Count > 0 ? ListingFormat.DateRange(Items.Min(i => i.StartDate), Items.Max(i => i.EndDate)) : "No items";
        public bool CanSubmit => Status == HarvestPlanStatus.Draft && Items.Count > 0 && !HasConflicts;
        public bool CanEdit => Status == HarvestPlanStatus.Draft;
        public bool CanDelete => Status != HarvestPlanStatus.Submitted;
        public bool CanClone => Items.Count > 0;

        public int PendingCount { get; set; }
        public int AcceptedCount { get; set; }
        public int PaidCount { get; set; }
        public int CompletedCount { get; set; }
        public int RejectedCount { get; set; }
        public int CancelledCount { get; set; }

        public List<HarvestPlanItemDetailViewModel> Items { get; set; } = new();

        /// <summary>Other draft plans for the farmer, allowing moving items between plans.</summary>
        public List<HarvestPlanOptionViewModel> OtherDraftPlans { get; set; } = new();
    }

    public class HarvestPlanItemDetailViewModel
    {
        public int Id { get; set; }
        public int HarvestPlanId { get; set; }
        public string ItemType { get; set; } = string.Empty; // "Equipment" or "Godown"
        public int ListingId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string CategoryOrType { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public string ListingDetailUrl { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DateRangeDisplay { get; set; } = string.Empty;
        public string DurationDisplay { get; set; } = string.Empty;

        public int Units { get; set; } = 1;
        public double Tons { get; set; } = 0;
        public string QuantityDisplay { get; set; } = string.Empty;
        public int MaxUnits { get; set; } = 1;
        public int MinRentalDays { get; set; } = 1;
        public double CapacityTons { get; set; } = 0;

        public string? AvailabilityMessage { get; set; }
        public bool IsAvailable { get; set; } = true;

        public decimal EstimatedGross { get; set; }
        public string GrossDisplay => $"৳{EstimatedGross:N0}";

        public string? Note { get; set; }

        // Linked booking information once submitted
        public int? BookingId { get; set; }
        public string? BookingStatus { get; set; }
        public string? BookingCode { get; set; }
        public bool CanPay { get; set; }
        public string? PayUrl { get; set; }
        public string? PassUrl { get; set; }
        public decimal? AgreedGross { get; set; }
    }

    public class HarvestPlanOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ItemCount { get; set; }
    }

    public class HarvestPlanItemInput
    {
        public int? PlanId { get; set; }
        public string? NewPlanName { get; set; }
        public string? ItemType { get; set; } // "Equipment" or "Godown"
        public int Id { get; set; }
        public int ListingId { get; set; }
        public int? EquipmentId { get; set; }
        public int? GodownId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? Units { get; set; }
        public double? RequestedCapacityTons { get; set; }
        public double? Tons { get; set; }
        public string? Note { get; set; }
        public string? BookingNotes { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public class HarvestPlanSubmitResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int PlanId { get; set; }
        public int CreatedCount { get; set; }
        public int FailedCount { get; set; }
        public int TotalCount { get; set; }
        public List<string> Errors { get; set; } = new();

        public static HarvestPlanSubmitResult Ok(int createdCount, int failedCount, int planId) =>
            new()
            {
                Success = true,
                Message = $"Harvest plan submitted successfully. {createdCount} request(s) sent to owners.",
                PlanId = planId,
                CreatedCount = createdCount,
                FailedCount = failedCount,
                TotalCount = createdCount + failedCount
            };

        public static HarvestPlanSubmitResult Partial(int createdCount, int failedCount, string message, List<string> errors, int planId) =>
            new()
            {
                Success = false,
                Message = message,
                PlanId = planId,
                CreatedCount = createdCount,
                FailedCount = failedCount,
                TotalCount = createdCount + failedCount,
                Errors = errors
            };

        public static HarvestPlanSubmitResult Fail(string message) =>
            new()
            {
                Success = false,
                Message = message
            };
    }

    public class HarvestPlanSummaryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Crop { get; set; }
        public string Status { get; set; } = HarvestPlanStatus.Draft;
        public int ItemCount { get; set; }
        public DateTime? EarliestStartDate { get; set; }
        public string TargetDateRange { get; set; } = string.Empty;
        public decimal EstimatedGross { get; set; }
    }
}
