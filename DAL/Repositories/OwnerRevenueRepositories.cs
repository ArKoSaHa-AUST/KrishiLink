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

        public abstract Task<IReadOnlyList<RevenueListing>> GetListingsAsync(string ownerId);

        public Task<WorkflowTransaction> BeginWorkflowAsync(IEnumerable<WorkflowLock> locks, CancellationToken ct = default) =>
            WorkflowTransaction.BeginAsync(Db, locks, ct);

        public abstract Task<IReadOnlyList<RevenueBooking>> GetBookingsAsync(string ownerId);

        public async Task<IReadOnlyList<Transaction>> GetPayoutsAsync(string ownerId) =>
            await Db.Transactions.AsNoTracking()
                .Where(t => t.UserId == ownerId)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

        public async Task<int> AddPayoutAsync(Transaction payout)
        {
            Db.Transactions.Add(payout);
            await Db.SaveChangesAsync();
            return payout.Id;
        }

        public abstract Task MarkBookingsPaidAsync(IEnumerable<int> bookingIds, int payoutId);

        public async Task<IReadOnlyList<BookingExpense>> GetExpensesAsync(string ownerId) =>
            await Db.BookingExpenses.AsNoTracking()
                .Where(e => e.OwnerId == ownerId && e.BookingType == _bookingType)
                .OrderByDescending(e => e.RecordedOn)
                .ToListAsync();

        public Task<BookingExpense?> GetExpenseAsync(string ownerId, int expenseId) =>
            Db.BookingExpenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.OwnerId == ownerId && e.BookingType == _bookingType);

        public async Task AddExpenseAsync(BookingExpense expense)
        {
            expense.BookingType = _bookingType;
            Db.BookingExpenses.Add(expense);
            await Db.SaveChangesAsync();
        }

        public async Task UpdateExpenseAsync(BookingExpense expense)
        {
            Db.BookingExpenses.Update(expense);
            await Db.SaveChangesAsync();
        }

        public async Task RemoveExpenseAsync(BookingExpense expense)
        {
            Db.BookingExpenses.Remove(expense);
            await Db.SaveChangesAsync();
        }
    }

    /// <summary>Pricing comes from the acceptance snapshot; legacy rows fall back to <see cref="BookingPricing.GodownGross"/>.</summary>
    public class GodownRevenueRepository : OwnerRevenueRepositoryBase, IGodownRevenueRepository
    {
        public GodownRevenueRepository(ApplicationDbContext db) : base(db, "Godown") { }

        public override async Task<IReadOnlyList<RevenueListing>> GetListingsAsync(string ownerId) =>
            await Db.Godowns.AsNoTracking()
                .Where(g => g.OwnerId == ownerId)
                .Select(g => new RevenueListing(g.Id, g.Name, g.Location, g.CapacityInTons))
                .ToListAsync();

        public override async Task<IReadOnlyList<RevenueBooking>> GetBookingsAsync(string ownerId) =>
            (await Db.GodownBookings.AsNoTracking()
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
                .ToListAsync())
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

        public override async Task MarkBookingsPaidAsync(IEnumerable<int> bookingIds, int payoutId)
        {
            var ids = bookingIds.ToList();
            foreach (var b in await Db.GodownBookings.Where(b => ids.Contains(b.Id)).ToListAsync()) b.PayoutId = payoutId;
            await Db.SaveChangesAsync();
        }
    }

    /// <summary>Pricing comes from the acceptance snapshot; legacy rows fall back to <see cref="BookingPricing.EquipmentGross"/>.</summary>
    public class EquipmentRevenueRepository : OwnerRevenueRepositoryBase, IEquipmentRevenueRepository
    {
        public EquipmentRevenueRepository(ApplicationDbContext db) : base(db, "Equipment") { }

        public override async Task<IReadOnlyList<RevenueListing>> GetListingsAsync(string ownerId) =>
            await Db.Equipment.AsNoTracking()
                .Where(e => e.OwnerId == ownerId)
                .Select(e => new RevenueListing(e.Id, e.Name, e.Location, e.Quantity))
                .ToListAsync();

        public override async Task<IReadOnlyList<RevenueBooking>> GetBookingsAsync(string ownerId) =>
            (await Db.EquipmentBookings.AsNoTracking()
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
                .ToListAsync())
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

        public override async Task MarkBookingsPaidAsync(IEnumerable<int> bookingIds, int payoutId)
        {
            var ids = bookingIds.ToList();
            foreach (var b in await Db.EquipmentBookings.Where(b => ids.Contains(b.Id)).ToListAsync()) b.PayoutId = payoutId;
            await Db.SaveChangesAsync();
        }
    }
}
