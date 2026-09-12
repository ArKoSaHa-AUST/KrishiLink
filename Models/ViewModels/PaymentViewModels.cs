namespace KrishiLink.Models.ViewModels
{
    /// <summary>Farmer checkout page for one accepted booking (/Bookings/Pay).</summary>
    public class PaymentCheckoutViewModel
    {
        public string BookingType { get; set; } = "Equipment";
        public int BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string DateRange { get; set; } = string.Empty;
        public string QuantityText { get; set; } = string.Empty;
        public string RateText { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string[] Methods { get; set; } = Array.Empty<string>();

        /// <summary>Prefilled with the farmer's phone number.</summary>
        public string? Account { get; set; }

        /// <summary>Failure reason of the most recent attempt, shown so the farmer knows why to retry.</summary>
        public string? LastFailureReason { get; set; }
    }

    /// <summary>Read-only projection of a <c>Payment</c> for the sandbox gateway page and confirmations.</summary>
    public record PaymentSummary(
        int Id,
        string BookingType,
        int BookingId,
        string ItemName,
        decimal Amount,
        string Method,
        string? PayerAccount,
        string Reference,
        string GatewayReference,
        string Status,
        DateTime? PaidOn,
        string? FailureReason);
}
