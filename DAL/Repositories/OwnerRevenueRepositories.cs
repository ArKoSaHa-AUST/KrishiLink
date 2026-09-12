using KrishiLink.BLL.Services;
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

        public int AddPayout(Transaction payout)
        {
            Db.Transactions.Add(payout);
            Db.SaveChanges();
            return payout.Id;
        }

        public abstract void MarkBookingsPaid(IEnumerable<int> bookingIds, int payoutId);

        public IReadOnlyList<BookingExpense> GetExpenses(string ownerId) =>
            Db.BookingExpenses.AsNoTracking()
                .Where(e => e.OwnerId == ownerId && e.BookingType == _bookingType)
                .OrderByDescending(e => e.RecordedOn)
                .ToList();

        public BookingExpense? GetExpense(string ownerId, int expenseId) =>
            Db.BookingExpenses.FirstOrDefault(e => e.Id == expenseId && e.OwnerId == ownerId && e.BookingType == _bookingType);

        public void AddExpense(BookingExpense expense)
        {
            expense.BookingType = _bookingType;
            Db.BookingExpenses.Add(expense);
            Db.SaveChanges();
        }

        public void UpdateExpense(BookingExpense expense)
        {
            Db.BookingExpenses.Update(expense);
            Db.SaveChanges();
        }

        public void RemoveExpense(BookingExpense expense)
        {
            Db.BookingExpenses.Remove(expense);
            Db.SaveChanges();
        }
    }

    /// <summary>Pricing comes from the acceptance snapshot; legacy rows fall back to <see cref="BookingPricing.GodownGross"/>.</summary>
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
                    b.Status,
                    b.PayoutId,
                    PayoutReference = b.Payout != null ? b.Payout.Reference : null,
                    b.AgreedRate,
                    b.AgreedGross,
                    b.CommissionRate,
                    b.PaidOn,
                    PaymentStatus = b.Payment != null ? b.Payment.Status : null,
                    PaymentReference = b.Payment != null ? b.Payment.Reference : null,
                    PaymentMethod = b.Payment != null ? b.Payment.Method : null
                })
                .AsEnumerable()
                .Select(b =>
                {
                    var rate = b.AgreedRate ?? b.PricePerTonPerMonth;
                    var months = ListingFormat.Months(b.StartDate, b.EndDate);
                    return new RevenueBooking(b.Id, b.GodownId, b.Name, b.Location, b.FarmerId, b.FarmerName, b.FarmerLocation,
                        b.StartDate, b.EndDate, b.Status,
                        Gross: b.AgreedGross ?? BookingPricing.GodownGross(b.StartDate, b.EndDate, b.StorageTons, rate),
                        CapacityUsed: b.StorageTons,
                        QuantityText: $"{b.StorageTons:N0} t × {months:0.##} mo",
                        RateText: $"৳{rate:N0} / t / mo",
                        PayoutId: b.PayoutId,
                        PayoutReference: b.PayoutReference,
                        IsPaid: b.PaymentStatus == PaymentStatus.Succeeded,
                        PaidOn: b.PaidOn,
                        PaymentReference: b.PaymentStatus == PaymentStatus.Succeeded ? b.PaymentReference : null,
                        PaymentMethod: b.PaymentStatus == PaymentStatus.Succeeded ? b.PaymentMethod : null,
                        CommissionRateSnapshot: b.CommissionRate ?? 0m);
                })
                .ToList();

        public override void MarkBookingsPaid(IEnumerable<int> bookingIds, int payoutId)
        {
            var ids = bookingIds.ToList();
            foreach (var b in Db.GodownBookings.Where(b => ids.Contains(b.Id))) b.PayoutId = payoutId;
            Db.SaveChanges();
        }
    }

    /// <summary>Pricing comes from the acceptance snapshot; legacy rows fall back to <see cref="BookingPricing.EquipmentGross"/>.</summary>
    public class EquipmentRevenueRepository : OwnerRevenueRepositoryBase, IEquipmentRevenueRepository
    {
        public EquipmentRevenueRepository(ApplicationDbContext db) : base(db, "Equipment") { }

        public override IReadOnlyList<RevenueListing> GetListings(string ownerId) =>
            Db.Equipment.AsNoTracking()
                .Where(e => e.OwnerId == ownerId)
                .Select(e => new RevenueListing(e.Id, e.Name, e.Location, e.Quantity))
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
                    b.Units,
                    b.FarmerId,
                    FarmerName = b.Farmer!.FullName,
                    FarmerLocation = b.Farmer.Location,
                    b.StartDate,
                    b.EndDate,
                    b.Status,
                    b.PayoutId,
                    PayoutReference = b.Payout != null ? b.Payout.Reference : null,
                    b.AgreedRate,
                    b.AgreedGross,
                    b.QuotedGross,
                    b.CommissionRate,
                    b.PaidOn,
                    PaymentStatus = b.Payment != null ? b.Payment.Status : null,
                    PaymentReference = b.Payment != null ? b.Payment.Reference : null,
                    PaymentMethod = b.Payment != null ? b.Payment.Method : null
                })
                .AsEnumerable()
                .Select(b =>
                {
                    var rate = b.AgreedRate ?? b.DailyRate;
                    var days = ListingFormat.InclusiveDays(b.StartDate, b.EndDate);
                    return new RevenueBooking(b.Id, b.EquipmentId, b.Name, b.Location, b.FarmerId, b.FarmerName, b.FarmerLocation,
                        b.StartDate, b.EndDate, b.Status,
                        Gross: BookingPricing.EquipmentGrossOf(b.AgreedGross, b.QuotedGross, b.StartDate, b.EndDate, rate, b.Units),
                        CapacityUsed: b.Units,
                        QuantityText: b.Units > 1 ? $"{b.Units} × {days} days" : (days == 1 ? "1 day" : $"{days} days"),
                        RateText: $"৳{rate:N0} / day",
                        PayoutId: b.PayoutId,
                        PayoutReference: b.PayoutReference,
                        IsPaid: b.PaymentStatus == PaymentStatus.Succeeded,
                        PaidOn: b.PaidOn,
                        PaymentReference: b.PaymentStatus == PaymentStatus.Succeeded ? b.PaymentReference : null,
                        PaymentMethod: b.PaymentStatus == PaymentStatus.Succeeded ? b.PaymentMethod : null,
                        CommissionRateSnapshot: b.CommissionRate ?? 0m);
                })
                .ToList();

        public override void MarkBookingsPaid(IEnumerable<int> bookingIds, int payoutId)
        {
            var ids = bookingIds.ToList();
            foreach (var b in Db.EquipmentBookings.Where(b => ids.Contains(b.Id))) b.PayoutId = payoutId;
            Db.SaveChanges();
        }
    }
}
