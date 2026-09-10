using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.DAL.Repositories
{
    /// <summary>Payout and expense access shared by the godown and equipment revenue repositories.</summary>
    public abstract class OwnerRevenueRepositoryBase : IOwnerRevenueRepository
    {
        protected readonly ApplicationDbContext Db;
        private readonly string _bookingType;

        protected OwnerRevenueRepositoryBase(ApplicationDbContext db, string bookingType)
        {
            Db = db;
            _bookingType = bookingType;
        }

        public abstract IReadOnlyList<RevenueListing> GetListings(string ownerId);

        public abstract IReadOnlyList<RevenueBooking> GetBookings(string ownerId);

        public IReadOnlyList<Transaction> GetPayouts(string ownerId) =>
            Db.Transactions.AsNoTracking()
                .Where(t => t.UserId == ownerId)
                .OrderByDescending(t => t.TransactionDate)
                .ToList();

        public void AddPayout(Transaction payout)
        {
            Db.Transactions.Add(payout);
            Db.SaveChanges();
        }

        public IReadOnlyList<BookingExpense> GetExpenses(string ownerId) =>
            Db.BookingExpenses.AsNoTracking()
                .Where(e => e.OwnerId == ownerId && e.BookingType == _bookingType)
                .OrderByDescending(e => e.RecordedOn)
                .ToList();

        public void AddExpense(BookingExpense expense)
        {
            expense.BookingType = _bookingType;
            Db.BookingExpenses.Add(expense);
            Db.SaveChanges();
        }
    }

    /// <summary>Pricing: storage tons × price per ton per month × pro-rata 30-day months.</summary>
    public class GodownRevenueRepository : OwnerRevenueRepositoryBase, IGodownRevenueRepository
    {
        public GodownRevenueRepository(ApplicationDbContext db) : base(db, "Godown") { }

        public override IReadOnlyList<RevenueListing> GetListings(string ownerId) =>
            Db.Godowns.AsNoTracking()
                .Where(g => g.OwnerId == ownerId)
                .Select(g => new RevenueListing(g.Id, g.Name, g.Location, g.CapacityInTons))
                .ToList();

        public override IReadOnlyList<RevenueBooking> GetBookings(string ownerId) =>
            Db.GodownBookings.AsNoTracking()
                .Where(b => b.Godown!.OwnerId == ownerId)
                .Select(b => new
                {
                    b.Id,
                    b.GodownId,
                    b.Godown!.Name,
                    b.Godown.Location,
                    b.Godown.PricePerTonPerMonth,
                    b.FarmerId,
                    FarmerName = b.Farmer!.FullName,
                    FarmerLocation = b.Farmer.Location,
                    b.StorageTons,
                    b.StartDate,
                    b.EndDate,
                    b.Status
                })
                .AsEnumerable()
                .Select(b =>
                {
                    var months = (b.EndDate - b.StartDate).TotalDays / 30.0;
                    return new RevenueBooking(b.Id, b.GodownId, b.Name, b.Location, b.FarmerId, b.FarmerName, b.FarmerLocation,
                        b.StartDate, b.EndDate, b.Status,
                        Gross: decimal.Round((decimal)b.StorageTons * b.PricePerTonPerMonth * (decimal)months, 0),
                        CapacityUsed: b.StorageTons,
                        QuantityText: $"{b.StorageTons:N0} t × {months:0.##} mo",
                        RateText: $"৳{b.PricePerTonPerMonth:N0} / t / mo");
                })
                .ToList();
    }

    /// <summary>Pricing: inclusive rental days × daily rate; each machine is one unit of capacity.</summary>
    public class EquipmentRevenueRepository : OwnerRevenueRepositoryBase, IEquipmentRevenueRepository
    {
        public EquipmentRevenueRepository(ApplicationDbContext db) : base(db, "Equipment") { }

        public override IReadOnlyList<RevenueListing> GetListings(string ownerId) =>
            Db.Equipment.AsNoTracking()
                .Where(e => e.OwnerId == ownerId)
                .Select(e => new RevenueListing(e.Id, e.Name, e.Location, 1))
                .ToList();

        public override IReadOnlyList<RevenueBooking> GetBookings(string ownerId) =>
            Db.EquipmentBookings.AsNoTracking()
                .Where(b => b.Equipment!.OwnerId == ownerId)
                .Select(b => new
                {
                    b.Id,
                    b.EquipmentId,
                    b.Equipment!.Name,
                    b.Equipment.Location,
                    b.Equipment.DailyRate,
                    b.FarmerId,
                    FarmerName = b.Farmer!.FullName,
                    FarmerLocation = b.Farmer.Location,
                    b.StartDate,
                    b.EndDate,
                    b.Status
                })
                .AsEnumerable()
                .Select(b =>
                {
                    var days = (b.EndDate - b.StartDate).Days + 1;
                    return new RevenueBooking(b.Id, b.EquipmentId, b.Name, b.Location, b.FarmerId, b.FarmerName, b.FarmerLocation,
                        b.StartDate, b.EndDate, b.Status,
                        Gross: days * b.DailyRate,
                        CapacityUsed: 1,
                        QuantityText: days == 1 ? "1 day" : $"{days} days",
                        RateText: $"৳{b.DailyRate:N0} / day");
                })
                .ToList();
    }
}
