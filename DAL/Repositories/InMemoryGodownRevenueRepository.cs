using KrishiLink.Models.Entities;

namespace KrishiLink.DAL.Repositories
{
    /// <summary>
    /// Sample godown revenue data. Mirrors the godowns and farmers used by the Godown Owner
    /// dashboard/requests pages so the pages tell a consistent story.
    /// Pricing: storage tons × price per ton per month × pro-rata 30-day months.
    /// </summary>
    public class InMemoryGodownRevenueRepository : InMemoryOwnerRevenueRepository, IGodownRevenueRepository
    {
        private static readonly Godown[] Godowns =
        {
            new() { Id = 1, Name = "Green Grain Cold Storage Facility", Location = "Dinajpur Sadar, Dinajpur", CapacityInTons = 300, PricePerTonPerMonth = 450 },
            new() { Id = 2, Name = "Dinajpur AgriHub Warehouse", Location = "Birganj, Dinajpur", CapacityInTons = 500, PricePerTonPerMonth = 300 },
            new() { Id = 3, Name = "Riverside Seed Vault", Location = "Parbatipur, Dinajpur", CapacityInTons = 120, PricePerTonPerMonth = 600 }
        };

        private static readonly Dictionary<string, ApplicationUser> Farmers = new[]
        {
            Farmer("f1", "Rahim Uddin", "Kaharole, Dinajpur"),
            Farmer("f2", "Salma Akter", "Bochaganj, Dinajpur"),
            Farmer("f3", "Motaleb Hossain", "Birol, Dinajpur"),
            Farmer("f4", "Abdul Halim", "Chirirbandar, Dinajpur"),
            Farmer("f5", "Shafiq Islam", "Khansama, Dinajpur"),
            Farmer("f6", "Jahanara Khatun", "Nawabganj, Dinajpur"),
            Farmer("f7", "Kamal Mia", "Fulbari, Dinajpur"),
            Farmer("f8", "Nasrin Begum", "Ghoraghat, Dinajpur")
        }.ToDictionary(f => f.Id);

        public InMemoryGodownRevenueRepository() : base(
            Godowns.Select(g => new RevenueListing(g.Id, g.Name, g.Location, g.CapacityInTons)).ToList(),
            new List<RevenueBooking>
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
            },
            new List<Transaction>
            {
                Payout(1, 210000, "bKash", "2026-06-05"),
                Payout(2, 180000, "Bank Transfer", "2026-07-05"),
                Payout(3, 165000, "bKash", "2026-08-05"),
                Payout(4, 95000, "bKash", DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd")),
                Payout(5, 60000, "Nagad", DateTime.Today.ToString("yyyy-MM-dd"), "Processing")
            },
            new List<BookingExpense>
            {
                Expense(1, 185, 2500, "Fumigation before intake", "2026-05-02"),
                Expense(2, 173, 6000, "Loading & unloading labour", "2026-04-16"),
                Expense(3, 176, 1800, "Generator fuel during outage", "2026-06-12")
            })
        {
        }

        private static RevenueBooking B(int id, int godownId, string farmerId, double tons, string start, string end, string status)
        {
            var g = Godowns.First(x => x.Id == godownId);
            var f = Farmers[farmerId];
            var startDate = DateTime.Parse(start);
            var endDate = DateTime.Parse(end);
            var months = (endDate - startDate).TotalDays / 30.0;
            return new RevenueBooking(id, g.Id, g.Name, g.Location, f.Id, f.FullName, f.Location, startDate, endDate, status,
                Gross: decimal.Round((decimal)tons * g.PricePerTonPerMonth * (decimal)months, 0),
                CapacityUsed: tons,
                QuantityText: $"{tons:N0} t × {months:0.##} mo",
                RateText: $"৳{g.PricePerTonPerMonth:N0} / t / mo");
        }
    }
}
