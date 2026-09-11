namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// A platform payout (settlement) to an owner. <see cref="Amount"/> is the net actually paid;
    /// <see cref="GrossAmount"/> is the booking revenue being settled and <see cref="Commission"/> what the platform kept.
    /// </summary>
    public class Transaction
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        /// <summary>Human-readable settlement reference, e.g. KL-PO-20260911-3F9A2C.</summary>
        public string Reference { get; set; } = string.Empty;

        public decimal GrossAmount { get; set; }
        public decimal Commission { get; set; }

        /// <summary>Net amount paid to the owner (gross − commission).</summary>
        public decimal Amount { get; set; }

        public string PaymentMethod { get; set; } = string.Empty;

        /// <summary>Destination wallet / account number the payout is sent to.</summary>
        public string? PayoutAccount { get; set; }

        /// <summary>"Processing" while the platform is transferring, then "Completed".</summary>
        public string Status { get; set; } = "Completed";
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    }
}
