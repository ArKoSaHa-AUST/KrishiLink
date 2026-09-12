namespace KrishiLink.Models.Entities
{
    public class GodownBooking : IPayableBooking
    {
        public int Id { get; set; }
        public int GodownId { get; set; }
        public Godown? Godown { get; set; }
        public string FarmerId { get; set; } = string.Empty;
        public ApplicationUser? Farmer { get; set; }
        public double StorageTons { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Note { get; set; }
        public string Status { get; set; } = BookingStatus.Pending;
        public string? RejectReason { get; set; }
        public DateTime RequestedOn { get; set; } = DateTime.UtcNow;

        /// <summary>When the owner last changed the status (accept/reject/complete).</summary>
        public DateTime? UpdatedOn { get; set; }

        /// <summary>Set when the farmer cancels the request.</summary>
        public DateTime? CancelledOn { get; set; }

        /// <summary>When the farmer last changed the booking dates / tonnage.</summary>
        public DateTime? ModifiedOn { get; set; }

        /// <summary>Number of times this booking has been modified (max 3).</summary>
        public int ModificationCount { get; set; } = 0;

        /// <summary>Summary of previous booking parameters before the last change.</summary>
        public string? PreviousDetails { get; set; }

        /// <summary>The payout that settled this booking's revenue; null while still unpaid.</summary>
        public int? PayoutId { get; set; }
        public Transaction? Payout { get; set; }

        // Price snapshot taken when the owner accepts, so later rate edits never change what was agreed.
        public decimal? AgreedRate { get; set; }
        public decimal? AgreedGross { get; set; }
        public decimal? CommissionRate { get; set; }
        public DateTime? CompletedOn { get; set; }

        /// <summary>The farmer's current (pending or succeeded) payment; cleared when an attempt fails so a retry can be made.</summary>
        public int? PaymentId { get; set; }
        public Payment? Payment { get; set; }
        public DateTime? PaidOn { get; set; }

        /// <summary>The farmer's review for this completed booking; null if not yet reviewed.</summary>
        public Review? Review { get; set; }

        // Loyalty Points & Promo Code Discount
        public decimal DiscountAmount { get; set; } = 0;
        public string? AppliedPromoCode { get; set; }
        public int PointsUsed { get; set; } = 0;
        public int PointsEarned { get; set; } = 0;
        public bool PointsAwarded { get; set; } = false;

        /// <summary>Optional link to a multi-item Harvest Plan if this booking was created from one.</summary>
        public int? HarvestPlanId { get; set; }
        public HarvestPlan? HarvestPlan { get; set; }

        /// <summary>Physical produce lots recorded in storage for this booking.</summary>
        public ICollection<StorageIntakeLot> IntakeLots { get; set; } = new List<StorageIntakeLot>();
    }
}
