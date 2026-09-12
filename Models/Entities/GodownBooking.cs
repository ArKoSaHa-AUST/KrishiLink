namespace KrishiLink.Models.Entities
{
    public class GodownBooking
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

        /// <summary>The payout that settled this booking's revenue; null while still unpaid.</summary>
        public int? PayoutId { get; set; }
        public Transaction? Payout { get; set; }

        /// <summary>The farmer's review for this completed booking; null if not yet reviewed.</summary>
        public Review? Review { get; set; }

        // Loyalty Points & Promo Code Discount
        public decimal DiscountAmount { get; set; } = 0;
        public string? AppliedPromoCode { get; set; }
        public int PointsUsed { get; set; } = 0;
        public int PointsEarned { get; set; } = 0;
        public bool PointsAwarded { get; set; } = false;
    }
}
