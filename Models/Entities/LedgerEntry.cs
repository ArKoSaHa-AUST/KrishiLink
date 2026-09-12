namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// Append-only double-entry record of every money movement. Each row moves <see cref="Amount"/> from
    /// <see cref="DebitAccount"/> to <see cref="CreditAccount"/>; rows are never edited or deleted, so
    /// balances are always derivable and the escrow conservation invariant can be verified at any time.
    /// </summary>
    public class LedgerEntry
    {
        public int Id { get; set; }
        public DateTime OccurredOn { get; set; } = DateTime.UtcNow;

        /// <summary>Account money leaves.</summary>
        public string DebitAccount { get; set; } = string.Empty;

        /// <summary>Account money enters.</summary>
        public string CreditAccount { get; set; } = string.Empty;

        /// <summary>Always positive.</summary>
        public decimal Amount { get; set; }

        /// <summary>One of <see cref="LedgerEntryType"/>.</summary>
        public string Type { get; set; } = string.Empty;

        public string BookingType { get; set; } = string.Empty;
        public int? BookingId { get; set; }
        public int? PaymentId { get; set; }
        public int? PayoutId { get; set; }

        /// <summary>Farmer for PaymentIn/Refund, owner for commission and payout rows.</summary>
        public string? UserId { get; set; }
        public string? Note { get; set; }
    }

    public static class LedgerAccount
    {
        public const string FarmerExternal = "FarmerExternal";
        public const string PlatformEscrow = "PlatformEscrow";
        public const string PlatformCommission = "PlatformCommission";
        public const string OwnerExternal = "OwnerExternal";
    }

    public static class LedgerEntryType
    {
        public const string PaymentIn = "PaymentIn";
        public const string Refund = "Refund";
        public const string CommissionEarned = "CommissionEarned";
        public const string CommissionReversed = "CommissionReversed";
        public const string PayoutOut = "PayoutOut";
        public const string PayoutReversed = "PayoutReversed";
    }
}
