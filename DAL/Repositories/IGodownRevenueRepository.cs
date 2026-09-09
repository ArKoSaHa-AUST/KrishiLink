using KrishiLink.Models.Entities;

namespace KrishiLink.DAL.Repositories
{
    /// <summary>
    /// Data access for the godown owner's revenue reporting.
    /// The current implementation is in-memory sample data; swap for an EF Core
    /// implementation once bookings/payouts are persisted.
    /// </summary>
    public interface IGodownRevenueRepository
    {
        IReadOnlyList<Godown> GetGodowns(string ownerId);

        /// <summary>Bookings for all of the owner's godowns, with Godown and Farmer populated.</summary>
        IReadOnlyList<GodownBooking> GetBookings(string ownerId);

        /// <summary>Platform payouts made to the owner.</summary>
        IReadOnlyList<Transaction> GetPayouts(string ownerId);

        IReadOnlyList<BookingExpense> GetExpenses(string ownerId);

        void AddExpense(BookingExpense expense);
    }
}
