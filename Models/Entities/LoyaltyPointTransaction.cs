namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// Types of loyalty point ledger transactions.
    /// </summary>
    public static class LoyaltyTransactionTypes
    {
        public const string Earned = "Earned";
        public const string Redeemed = "Redeemed";
        public const string Bonus = "Bonus";
        public const string Refunded = "Refunded";
        public const string VoucherGenerated = "VoucherGenerated";
    }

    /// <summary>
    /// Transaction ledger entry recording every point earn, redemption, discount application, and refund.
    /// </summary>
    public class LoyaltyPointTransaction
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        /// <summary>Points change (positive for earned/refunded, negative for redeemed/discounted).</summary>
        public int Points { get; set; }

        /// <summary>Transaction category (Earned, Redeemed, Bonus, Refunded, VoucherGenerated).</summary>
        public string Type { get; set; } = LoyaltyTransactionTypes.Earned;

        /// <summary>Human-readable description of how the points were earned or redeemed.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Associated booking type (Equipment or Godown) if linked to a booking.</summary>
        public string? BookingType { get; set; }

        /// <summary>ID of the associated equipment or godown booking.</summary>
        public int? BookingId { get; set; }

        /// <summary>Display reference code of the associated booking (e.g., #EQ-1042 or #GD-2015).</summary>
        public string? BookingCode { get; set; }

        /// <summary>Associated promo code / voucher code if used or generated.</summary>
        public string? PromoCode { get; set; }

        /// <summary>The monetary discount value (in ৳) corresponding to redeemed points.</summary>
        public decimal? DiscountAmount { get; set; }

        /// <summary>The total amount spent (in ৳) on the booking that earned these points.</summary>
        public decimal? AmountSpent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
