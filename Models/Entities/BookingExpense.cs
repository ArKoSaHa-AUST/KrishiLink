namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// An owner-recorded cost (fuel, labour, fumigation, etc.) against a godown booking,
    /// used to show net profit alongside gross revenue.
    /// </summary>
    public class BookingExpense
    {
        public int Id { get; set; }
        public int GodownBookingId { get; set; }
        public GodownBooking? GodownBooking { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime RecordedOn { get; set; } = DateTime.UtcNow;
    }
}
