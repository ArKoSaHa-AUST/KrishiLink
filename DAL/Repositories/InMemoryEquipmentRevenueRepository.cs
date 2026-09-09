using KrishiLink.Models.Entities;

namespace KrishiLink.DAL.Repositories
{
    /// <summary>
    /// Sample equipment revenue data. Mirrors the machines and farmers used by the Equipment Owner
    /// dashboard/requests pages. Pricing: inclusive rental days × daily rate; each machine is one unit of capacity.
    /// </summary>
    public class InMemoryEquipmentRevenueRepository : InMemoryOwnerRevenueRepository, IEquipmentRevenueRepository
    {
        private const string Yard = "Bogra Sadar, Bogra";

        private static readonly Equipment[] Machines =
        {
            new() { Id = 1, Name = "Mahindra 575 DI Heavy Tractor", Category = "Tractor", DailyRate = 1500 },
            new() { Id = 2, Name = "Kubota DC-70 Combine Harvester", Category = "Harvester", DailyRate = 4200 },
            new() { Id = 3, Name = "ACI Power Tiller 12HP", Category = "Tiller", DailyRate = 800 },
            new() { Id = 4, Name = "Honda WB30X Irrigation Pump", Category = "Irrigation", DailyRate = 350 },
            new() { Id = 5, Name = "TAFE 45DI Rotavator", Category = "Tiller", DailyRate = 950 }
        };

        private static readonly Dictionary<string, ApplicationUser> Farmers = new[]
        {
            Farmer("f1", "Rahim Uddin", "Shibganj, Bogra"),
            Farmer("f2", "Karim Mia", "Sherpur, Bogra"),
            Farmer("f3", "Fatema Begum", "Gabtali, Bogra"),
            Farmer("f4", "Abdul Halim", "Kahaloo, Bogra"),
            Farmer("f5", "Shafiq Islam", "Dupchanchia, Bogra"),
            Farmer("f6", "Jahanara Khatun", "Adamdighi, Bogra"),
            Farmer("f7", "Motaleb Hossain", "Sonatala, Bogra")
        }.ToDictionary(f => f.Id);

        public InMemoryEquipmentRevenueRepository() : base(
            Machines.Select(m => new RevenueListing(m.Id, m.Name, Yard, Capacity: 1)).ToList(),
            new List<RevenueBooking>
            {
                // Aman harvest & Rabi land preparation
                B(40, 2, "f2", "2025-11-10", "2025-11-16", "Completed"),
                B(42, 1, "f4", "2025-11-20", "2025-11-23", "Completed"),
                B(45, 3, "f3", "2025-12-05", "2025-12-07", "Completed"),

                // Boro irrigation season
                B(48, 4, "f7", "2026-01-10", "2026-01-25", "Completed"),
                B(50, 1, "f1", "2026-01-15", "2026-01-18", "Completed"),
                B(53, 5, "f5", "2026-02-01", "2026-02-03", "Cancelled"),
                B(55, 4, "f2", "2026-02-10", "2026-02-28", "Completed"),

                // Boro harvest peak (Apr–May)
                B(58, 2, "f6", "2026-04-20", "2026-04-26", "Completed"),
                B(60, 2, "f1", "2026-05-01", "2026-05-08", "Completed"),
                B(63, 1, "f7", "2026-05-12", "2026-05-15", "Completed"),
                B(66, 3, "f4", "2026-06-05", "2026-06-08", "Completed"),
                B(70, 5, "f3", "2026-06-20", "2026-06-22", "Completed"),
                B(74, 1, "f2", "2026-07-01", "2026-07-05", "Completed"),
                B(78, 5, "f6", "2026-07-20", "2026-07-21", "Cancelled"),
                B(82, 3, "f1", "2026-07-15", "2026-07-18", "Completed"),
                B(85, 4, "f7", "2026-08-01", "2026-08-10", "Completed"),
                B(88, 1, "f5", "2026-08-15", "2026-08-18", "Completed"),
                B(93, 2, "f7", "2026-09-01", "2026-09-04", "Completed"),

                // Current: accepted, pending, rejected (matches the Requests page)
                B(91, 2, "f6", "2026-08-20", "2026-08-25", "Rejected"),
                B(97, 5, "f5", "2026-08-28", "2026-08-29", "Accepted"),
                B(96, 1, "f4", "2026-09-09", "2026-09-11", "Accepted"),
                B(101, 2, "f1", "2026-09-02", "2026-09-06", "Pending"),
                B(102, 3, "f2", "2026-09-05", "2026-09-07", "Pending"),
                B(103, 1, "f3", "2026-09-10", "2026-09-12", "Pending")
            },
            new List<Transaction>
            {
                Payout(1, 45000, "bKash", "2026-06-05"),
                Payout(2, 35000, "Nagad", "2026-07-05"),
                Payout(3, 20000, "bKash", "2026-08-05"),
                Payout(4, 15000, "bKash", DateTime.Today.AddDays(-3).ToString("yyyy-MM-dd")),
                Payout(5, 10000, "Bank Transfer", DateTime.Today.ToString("yyyy-MM-dd"), "Processing")
            },
            new List<BookingExpense>
            {
                Expense(1, 60, 2500, "Diesel for harvester", "2026-05-02"),
                Expense(2, 74, 600, "Tractor oil change", "2026-07-06")
            })
        {
        }

        private static RevenueBooking B(int id, int equipmentId, string farmerId, string start, string end, string status)
        {
            var m = Machines.First(x => x.Id == equipmentId);
            var f = Farmers[farmerId];
            var startDate = DateTime.Parse(start);
            var endDate = DateTime.Parse(end);
            var days = (endDate - startDate).Days + 1;
            return new RevenueBooking(id, m.Id, m.Name, Yard, f.Id, f.FullName, f.Location, startDate, endDate, status,
                Gross: days * m.DailyRate,
                CapacityUsed: 1,
                QuantityText: days == 1 ? "1 day" : $"{days} days",
                RateText: $"৳{m.DailyRate:N0} / day");
        }
    }
}
