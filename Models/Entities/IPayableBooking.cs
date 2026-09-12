namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// The money-related shape shared by equipment and godown bookings so payment, ledger and
    /// workflow code can be written once. Prices are snapshotted at acceptance and never recomputed.
    /// </summary>
    public interface IPayableBooking
    {
        int Id { get; }
        string FarmerId { get; }
        DateTime StartDate { get; }
        DateTime EndDate { get; }
        string Status { get; set; }
        DateTime? UpdatedOn { get; set; }
        DateTime? CancelledOn { get; set; }
        int ModificationCount { get; }
        string? PreviousDetails { get; }

        decimal? AgreedRate { get; set; }
        decimal? AgreedGross { get; set; }
        decimal? CommissionRate { get; set; }
        DateTime? CompletedOn { get; set; }

        int? PaymentId { get; set; }
        Payment? Payment { get; set; }
        DateTime? PaidOn { get; set; }

        int? PayoutId { get; set; }
    }
}
