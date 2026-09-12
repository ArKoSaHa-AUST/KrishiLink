namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// An owner-recorded cost (fuel, labour, fumigation, etc.) against a godown or equipment booking,
    /// or a general facility/operating expense, used to show net profit alongside gross revenue.
    /// </summary>
    public class BookingExpense
    {
        public int Id { get; set; }

        /// <summary>"Equipment" or "Godown" — booking ids are only unique within their own table.</summary>
        public string BookingType { get; set; } = string.Empty;

        /// <summary>Nullable: null indicates a general facility/operating expense not tied to a single booking.</summary>
        public int? BookingId { get; set; }

        /// <summary>Optional attribution to a specific listing (godown or equipment) for general expenses.</summary>
        public int? ListingId { get; set; }

        public string OwnerId { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string Category { get; set; } = ExpenseCategories.Other;

        public string Note { get; set; } = string.Empty;

        public DateTime ExpenseDate { get; set; } = DateTime.UtcNow.Date;

        public DateTime RecordedOn { get; set; } = DateTime.UtcNow;
    }
}
