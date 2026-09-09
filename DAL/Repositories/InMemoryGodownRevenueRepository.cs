using KrishiLink.Models.Entities;

namespace KrishiLink.DAL.Repositories
{
    /// <summary>
    /// Sample-data implementation shared until DB wiring. Mirrors the godowns and farmers used by
    /// the Godown Owner dashboard/requests pages so the pages tell a consistent story.
    /// Registered as a singleton so expenses added during a session persist across requests.
    /// </summary>
    public class InMemoryGodownRevenueRepository : IGodownRevenueRepository
    {
        private readonly object _sync = new();
        private readonly List<Godown> _godowns;
        private readonly List<GodownBooking> _bookings;
        private readonly List<Transaction> _payouts;
        private readonly List<BookingExpense> _expenses;

        public InMemoryGodownRevenueRepository()
        {
            _godowns = new List<Godown>
            {
                new() { Id = 1, Name = "Green Grain Cold Storage Facility", Location = "Dinajpur Sadar, Dinajpur", CapacityInTons = 300, PricePerTonPerMonth = 450 },
                new() { Id = 2, Name = "Dinajpur AgriHub Warehouse", Location = "Birganj, Dinajpur", CapacityInTons = 500, PricePerTonPerMonth = 300 },
                new() { Id = 3, Name = "Riverside Seed Vault", Location = "Parbatipur, Dinajpur", CapacityInTons = 120, PricePerTonPerMonth = 600 }
            };

            var farmers = new Dictionary<string, ApplicationUser>
            {
                ["f1"] = new() { Id = "f1", FullName = "Rahim Uddin", Location = "Kaharole, Dinajpur" },
                ["f2"] = new() { Id = "f2", FullName = "Salma Akter", Location = "Bochaganj, Dinajpur" },
                ["f3"] = new() { Id = "f3", FullName = "Motaleb Hossain", Location = "Birol, Dinajpur" },
                ["f4"] = new() { Id = "f4", FullName = "Abdul Halim", Location = "Chirirbandar, Dinajpur" },
                ["f5"] = new() { Id = "f5", FullName = "Shafiq Islam", Location = "Khansama, Dinajpur" },
                ["f6"] = new() { Id = "f6", FullName = "Jahanara Khatun", Location = "Nawabganj, Dinajpur" },
                ["f7"] = new() { Id = "f7", FullName = "Kamal Mia", Location = "Fulbari, Dinajpur" },
                ["f8"] = new() { Id = "f8", FullName = "Nasrin Begum", Location = "Ghoraghat, Dinajpur" }
            };

            GodownBooking B(int id, int godownId, string farmerId, double tons, string start, string end, string status) => new()
            {
                Id = id,
                GodownId = godownId,
                Godown = _godowns.First(g => g.Id == godownId),
                FarmerId = farmerId,
                Farmer = farmers[farmerId],
                StorageTons = tons,
                StartDate = DateTime.Parse(start),
                EndDate = DateTime.Parse(end),
                Status = status
            };

            _bookings = new List<GodownBooking>
            {
                // Previous Aman season (Sep–Dec) and winter storage
                B(150, 2, "f7", 100, "2025-09-15", "2025-11-15", "Completed"),
                B(152, 1, "f8", 40, "2025-10-01", "2025-12-01", "Completed"),
                B(155, 2, "f1", 150, "2025-11-20", "2026-02-20", "Completed"),
                B(157, 3, "f6", 50, "2025-12-01", "2026-03-01", "Completed"),
                B(160, 1, "f3", 30, "2026-01-10", "2026-02-10", "Cancelled"),
                B(163, 2, "f5", 60, "2026-01-15", "2026-03-15", "Completed"),
                B(166, 1, "f2", 80, "2026-02-01", "2026-04-01", "Completed"),
                B(170, 3, "f7", 20, "2026-03-01", "2026-04-01", "Completed"),

                // Boro harvest peak (Apr–Jun)
                B(173, 2, "f4", 200, "2026-04-15", "2026-07-15", "Completed"),
                B(182, 1, "f1", 45, "2026-04-10", "2026-07-10", "Completed"),
                B(185, 2, "f3", 80, "2026-05-01", "2026-08-01", "Completed"),
                B(176, 1, "f8", 70, "2026-05-20", "2026-08-20", "Completed"),
                B(178, 3, "f2", 25, "2026-06-01", "2026-06-20", "Cancelled"),
                B(180, 2, "f7", 60, "2026-06-10", "2026-08-10", "Completed"),
                B(188, 1, "f4", 50, "2026-07-01", "2026-09-01", "Completed"),

                // Current: ongoing, upcoming, pending, rejected (matches the Requests page)
                B(190, 3, "f8", 40, "2026-08-01", "2026-11-01", "Accepted"),
                B(191, 3, "f6", 30, "2026-08-18", "2026-09-18", "Rejected"),
                B(196, 2, "f4", 120, "2026-08-20", "2026-11-20", "Accepted"),
                B(197, 1, "f5", 60, "2026-08-25", "2026-10-25", "Accepted"),
                B(199, 2, "f6", 30, "2026-10-01", "2026-12-31", "Accepted"),
                B(201, 1, "f1", 25, "2026-09-02", "2026-11-30", "Pending"),
                B(202, 2, "f2", 40, "2026-09-05", "2026-12-05", "Pending"),
                B(203, 1, "f3", 200, "2026-09-10", "2026-10-10", "Pending")
            };

            _payouts = new List<Transaction>
            {
                new() { Id = 1, Amount = 210000, PaymentMethod = "bKash", Status = "Completed", TransactionDate = DateTime.Parse("2026-06-05") },
                new() { Id = 2, Amount = 180000, PaymentMethod = "Bank Transfer", Status = "Completed", TransactionDate = DateTime.Parse("2026-07-05") },
                new() { Id = 3, Amount = 165000, PaymentMethod = "bKash", Status = "Completed", TransactionDate = DateTime.Parse("2026-08-05") },
                new() { Id = 4, Amount = 95000, PaymentMethod = "bKash", Status = "Completed", TransactionDate = DateTime.Today.AddDays(-2) },
                new() { Id = 5, Amount = 60000, PaymentMethod = "Nagad", Status = "Processing", TransactionDate = DateTime.Today }
            };

            _expenses = new List<BookingExpense>
            {
                new() { Id = 1, GodownBookingId = 185, Amount = 2500, Note = "Fumigation before intake", RecordedOn = DateTime.Parse("2026-05-02") },
                new() { Id = 2, GodownBookingId = 173, Amount = 6000, Note = "Loading & unloading labour", RecordedOn = DateTime.Parse("2026-04-16") },
                new() { Id = 3, GodownBookingId = 176, Amount = 1800, Note = "Generator fuel during outage", RecordedOn = DateTime.Parse("2026-06-12") }
            };
        }

        // Sample data is not owner-scoped yet; the ownerId parameter is kept so callers are DB-ready.
        public IReadOnlyList<Godown> GetGodowns(string ownerId) => _godowns;

        public IReadOnlyList<GodownBooking> GetBookings(string ownerId) => _bookings;

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
