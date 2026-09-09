using KrishiLink.Models.Entities;

namespace KrishiLink.DAL.Repositories
{
    /// <summary>
    /// Shared sample-data plumbing (payouts, expenses) for the in-memory revenue repositories used until DB wiring.
    /// Registered as singletons so expenses added during a session persist across requests.
    /// Sample data is not owner-scoped yet; the ownerId parameters are kept so callers are DB-ready.
    /// </summary>
    public abstract class InMemoryOwnerRevenueRepository : IOwnerRevenueRepository
    {
        private readonly object _sync = new();
        private readonly List<RevenueListing> _listings;
        private readonly List<RevenueBooking> _bookings;
        private readonly List<Transaction> _payouts;
        private readonly List<BookingExpense> _expenses;

        protected InMemoryOwnerRevenueRepository(List<RevenueListing> listings, List<RevenueBooking> bookings,
            List<Transaction> payouts, List<BookingExpense> expenses)
        {
            _listings = listings;
            _bookings = bookings;
            _payouts = payouts;
            _expenses = expenses;
        }

        protected static ApplicationUser Farmer(string id, string name, string location) =>
            new() { Id = id, FullName = name, Location = location };

        protected static Transaction Payout(int id, decimal amount, string method, string date, string status = "Completed") =>
            new() { Id = id, Amount = amount, PaymentMethod = method, Status = status, TransactionDate = DateTime.Parse(date) };

        protected static BookingExpense Expense(int id, int bookingId, decimal amount, string note, string date) =>
            new() { Id = id, BookingId = bookingId, Amount = amount, Note = note, RecordedOn = DateTime.Parse(date) };

        public IReadOnlyList<RevenueListing> GetListings(string ownerId) => _listings;

        public IReadOnlyList<RevenueBooking> GetBookings(string ownerId) => _bookings;

        public IReadOnlyList<Transaction> GetPayouts(string ownerId) => _payouts;

        public IReadOnlyList<BookingExpense> GetExpenses(string ownerId)
        {
            lock (_sync) return _expenses.ToList();
        }

        public void AddExpense(BookingExpense expense)
        {
            lock (_sync)
            {
                expense.Id = _expenses.Count == 0 ? 1 : _expenses.Max(e => e.Id) + 1;
                _expenses.Add(expense);
            }
        }
    }
}
