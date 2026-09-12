namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// A farmer's payment into platform escrow for one accepted booking. One row per attempt: a failed
    /// attempt stays for history and the booking's <c>PaymentId</c> is cleared so the farmer can retry.
    /// </summary>
    public class Payment
    {
        public int Id { get; set; }

        /// <summary>"Equipment" or "Godown" — booking ids are only unique within their own table.</summary>
        public string BookingType { get; set; } = string.Empty;
        public int BookingId { get; set; }

        public string FarmerId { get; set; } = string.Empty;
        public ApplicationUser? Farmer { get; set; }

        /// <summary>What the farmer paid — always the booking's AgreedGross.</summary>
        public decimal Amount { get; set; }

        /// <summary>bKash | Nagad | Rocket | Card</summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>Wallet / card number the farmer paid from (as entered; never validated against a bank).</summary>
        public string? PayerAccount { get; set; }

        /// <summary>Human-readable reference, e.g. KL-PM-20260912-7A3F1C.</summary>
        public string Reference { get; set; } = string.Empty;

        /// <summary>Provider transaction id; the simulated gateway issues "SIM-" + 10 hex characters.</summary>
        public string? GatewayReference { get; set; }

        /// <summary>Pending | Succeeded | Failed | Refunded</summary>
        public string Status { get; set; } = PaymentStatus.Pending;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? PaidOn { get; set; }
        public DateTime? RefundedOn { get; set; }
        public string? FailureReason { get; set; }
    }
}
