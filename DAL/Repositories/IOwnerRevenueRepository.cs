using KrishiLink.Models.Entities;

namespace KrishiLink.DAL.Repositories
{
    /// <summary>A rentable listing (godown or equipment) in the shape the revenue report needs.</summary>
    public record RevenueListing(int Id, string Name, string Location, double Capacity);

    /// <summary>
    /// A booking in the shape the revenue report needs. Domain-specific pricing (tons × rate × months,
    /// or days × daily rate) is resolved by the repository so the service stays listing-agnostic.
    /// </summary>
    public record RevenueBooking(
        int Id,
        int ListingId,
        string ListingName,
        string ListingLocation,
        string CustomerId,
        string CustomerName,
        string? CustomerLocation,
        DateTime StartDate,
        DateTime EndDate,
        string Status,
        decimal Gross,
        // Booked capacity in the listing's capacity unit, used for utilization (tons for godowns, 1 for equipment)
        double CapacityUsed,
        // Human-readable quantity, e.g. "80 t × 3.07 mo" or "5 days"
        string QuantityText,
        // Human-readable rate, e.g. "৳300 / t / mo" or "৳1,500 / day"
        string RateText);

    /// <summary>
    /// Data access for an owner's revenue reporting (godown or equipment).
    /// Current implementations are in-memory sample data; swap for EF Core once bookings/payouts are persisted.
    /// </summary>
    public interface IOwnerRevenueRepository
    {
        IReadOnlyList<RevenueListing> GetListings(string ownerId);
        IReadOnlyList<RevenueBooking> GetBookings(string ownerId);

        /// <summary>Platform payouts made to the owner.</summary>
        IReadOnlyList<Transaction> GetPayouts(string ownerId);

        IReadOnlyList<BookingExpense> GetExpenses(string ownerId);
        void AddExpense(BookingExpense expense);
    }

    public interface IGodownRevenueRepository : IOwnerRevenueRepository { }

    public interface IEquipmentRevenueRepository : IOwnerRevenueRepository { }
}
