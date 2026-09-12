using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using KrishiLink.Models.Entities;

namespace KrishiLink.DAL
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Equipment> Equipment { get; set; } = null!;
        public DbSet<EquipmentBooking> EquipmentBookings { get; set; } = null!;
        public DbSet<EquipmentBlockedDate> EquipmentBlockedDates { get; set; } = null!;
        public DbSet<EquipmentRateRule> EquipmentRateRules { get; set; } = null!;
        public DbSet<Godown> Godowns { get; set; } = null!;
        public DbSet<GodownBooking> GodownBookings { get; set; } = null!;
        public DbSet<GodownBlockedDate> GodownBlockedDates { get; set; } = null!;
        public DbSet<BookingExpense> BookingExpenses { get; set; } = null!;
        public DbSet<Crop> Crops { get; set; } = null!;
        public DbSet<CropRecommendation> CropRecommendations { get; set; } = null!;
        public DbSet<WeatherData> WeatherData { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<OwnerVerificationRequest> VerificationRequests { get; set; } = null!;
        public DbSet<EquipmentMaintenanceRecord> EquipmentMaintenanceRecords { get; set; } = null!;
        public DbSet<LoyaltyPointTransaction> LoyaltyPointTransactions { get; set; } = null!;
        public DbSet<CropCalendarEntry> CropCalendarEntries { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<LedgerEntry> LedgerEntries { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(u =>
            {
                u.Property(x => x.District).HasMaxLength(60);
                u.Property(x => x.Specialization).HasMaxLength(60);
                u.Property(x => x.VerificationStatus).HasMaxLength(30).HasDefaultValue("Unverified");
                u.Property(x => x.NidNumber).HasMaxLength(30);
                u.Property(x => x.NidFrontImagePath).HasMaxLength(255);
                u.Property(x => x.NidBackImagePath).HasMaxLength(255);
                u.Property(x => x.TradeLicenseImagePath).HasMaxLength(255);
                u.Property(x => x.VerificationRejectionReason).HasMaxLength(500);
                u.Property(x => x.VerificationNotes).HasMaxLength(500);
                u.Property(x => x.OwnerAverageRating).HasDefaultValue(0.0);
                u.Property(x => x.OwnerReviewCount).HasDefaultValue(0);
                u.HasIndex(x => x.IsVerified);
                u.HasIndex(x => x.VerificationStatus);
            });

            builder.Entity<Equipment>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100);
                e.Property(x => x.Category).HasMaxLength(50);
                e.Property(x => x.Location).HasMaxLength(150);
                e.Property(x => x.District).HasMaxLength(60);
                e.Property(x => x.DailyRate).HasPrecision(18, 2);
                e.Property(x => x.HourlyRate).HasPrecision(18, 2);
                e.Property(x => x.MinRentalDays).HasDefaultValue(1);
                e.Property(x => x.Quantity).HasDefaultValue(1);
                e.Property(x => x.AverageRating).HasDefaultValue(0.0);
                e.Property(x => x.ReviewCount).HasDefaultValue(0);
                e.HasIndex(x => x.OwnerId);
                e.HasIndex(x => x.District);
                e.HasIndex(x => new { x.Latitude, x.Longitude });
                e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<EquipmentBooking>(b =>
            {
                b.Property(x => x.Status).HasMaxLength(20);
                b.Property(x => x.Units).HasDefaultValue(1);
                b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
                b.Property(x => x.AppliedPromoCode).HasMaxLength(50);
                b.Property(x => x.QuotedGross).HasPrecision(18, 2).HasDefaultValue(0m);
                b.Property(x => x.PricingNote).HasMaxLength(200);
                b.Property(x => x.AgreedRate).HasPrecision(18, 2);
                b.Property(x => x.AgreedGross).HasPrecision(18, 2);
                b.Property(x => x.CommissionRate).HasPrecision(5, 4);
                b.HasIndex(x => new { x.EquipmentId, x.Status });
                b.HasOne(x => x.Equipment).WithMany(x => x.Bookings).HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(x => x.Farmer).WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(x => x.Payout).WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<EquipmentBlockedDate>(d =>
            {
                d.Property(x => x.Reason).HasMaxLength(100);
                d.HasIndex(x => new { x.EquipmentId, x.Date }).IsUnique();
                d.HasOne(x => x.Equipment).WithMany(x => x.BlockedDates).HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<EquipmentRateRule>(r =>
            {
                r.Property(x => x.Kind).HasMaxLength(10).IsRequired();
                r.Property(x => x.Name).HasMaxLength(60).IsRequired();
                r.Property(x => x.DailyRate).HasPrecision(18, 2);
                r.HasIndex(x => new { x.EquipmentId, x.IsActive });
                r.HasOne(x => x.Equipment).WithMany(e => e.RateRules).HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Godown>(g =>
            {
                g.Property(x => x.Name).HasMaxLength(120);
                g.Property(x => x.StorageType).HasMaxLength(50);
                g.Property(x => x.Location).HasMaxLength(150);
                g.Property(x => x.District).HasMaxLength(60);
                g.Property(x => x.PricePerTonPerMonth).HasPrecision(18, 2);
                g.Property(x => x.AverageRating).HasDefaultValue(0.0);
                g.Property(x => x.ReviewCount).HasDefaultValue(0);
                g.HasIndex(x => x.OwnerId);
                g.HasIndex(x => x.District);
                g.HasIndex(x => new { x.Latitude, x.Longitude });
                g.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<GodownBooking>(b =>
            {
                b.Property(x => x.Status).HasMaxLength(20);
                b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
                b.Property(x => x.AppliedPromoCode).HasMaxLength(50);
                b.Property(x => x.AgreedRate).HasPrecision(18, 2);
                b.Property(x => x.AgreedGross).HasPrecision(18, 2);
                b.Property(x => x.CommissionRate).HasPrecision(5, 4);
                b.HasIndex(x => new { x.GodownId, x.Status });
                b.HasOne(x => x.Godown).WithMany(x => x.Bookings).HasForeignKey(x => x.GodownId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(x => x.Farmer).WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(x => x.Payout).WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<GodownBlockedDate>(d =>
            {
                d.Property(x => x.Reason).HasMaxLength(100);
                d.HasIndex(x => new { x.GodownId, x.Date }).IsUnique();
                d.HasOne(x => x.Godown).WithMany(x => x.BlockedDates).HasForeignKey(x => x.GodownId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<BookingExpense>(x =>
            {
                x.Property(e => e.BookingType).HasMaxLength(20);
                x.Property(e => e.Note).HasMaxLength(200);
                x.Property(e => e.Amount).HasPrecision(18, 2);
                x.HasIndex(e => new { e.OwnerId, e.BookingType });
            });

            builder.Entity<Transaction>(t =>
            {
                t.Property(x => x.Reference).HasMaxLength(30);
                t.Property(x => x.ListingType).HasMaxLength(30);
                t.Property(x => x.PaymentMethod).HasMaxLength(30);
                t.Property(x => x.PayoutAccount).HasMaxLength(40);
                t.Property(x => x.Status).HasMaxLength(20);
                t.Property(x => x.FailureReason).HasMaxLength(200);
                t.Property(x => x.GrossAmount).HasPrecision(18, 2);
                t.Property(x => x.Commission).HasPrecision(18, 2);
                t.Property(x => x.Amount).HasPrecision(18, 2);
                t.HasIndex(x => x.UserId);
                t.HasIndex(x => x.Reference).IsUnique();
                t.HasIndex(x => new { x.Status, x.TransactionDate });
            });

            builder.Entity<Payment>(p =>
            {
                p.Property(x => x.BookingType).HasMaxLength(20);
                p.Property(x => x.Method).HasMaxLength(20);
                p.Property(x => x.PayerAccount).HasMaxLength(40);
                p.Property(x => x.Reference).HasMaxLength(30);
                p.Property(x => x.GatewayReference).HasMaxLength(40);
                p.Property(x => x.Status).HasMaxLength(20);
                p.Property(x => x.FailureReason).HasMaxLength(200);
                p.Property(x => x.Amount).HasPrecision(18, 2);
                p.HasIndex(x => x.Reference).IsUnique();
                p.HasIndex(x => x.GatewayReference);
                p.HasIndex(x => new { x.BookingType, x.BookingId });
                p.HasOne(x => x.Farmer).WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<LedgerEntry>(l =>
            {
                l.Property(x => x.DebitAccount).HasMaxLength(30);
                l.Property(x => x.CreditAccount).HasMaxLength(30);
                l.Property(x => x.Type).HasMaxLength(30);
                l.Property(x => x.BookingType).HasMaxLength(20);
                l.Property(x => x.UserId).HasMaxLength(450);
                l.Property(x => x.Note).HasMaxLength(200);
                l.Property(x => x.Amount).HasPrecision(18, 2);
                l.HasIndex(x => x.DebitAccount);
                l.HasIndex(x => x.CreditAccount);
                l.HasIndex(x => new { x.BookingType, x.BookingId });
                l.HasIndex(x => x.PaymentId);
                l.HasIndex(x => x.PayoutId);
            });

            builder.Entity<Review>(r =>
            {
                r.Property(x => x.Comment).HasMaxLength(1000);
                r.Property(x => x.OwnerReply).HasMaxLength(1000);
                r.Property(x => x.BookingType).HasMaxLength(20);
                r.HasIndex(x => new { x.EquipmentId, x.CreatedAt });
                r.HasIndex(x => new { x.GodownId, x.CreatedAt });
                r.HasIndex(x => x.EquipmentBookingId).IsUnique();
                r.HasIndex(x => x.GodownBookingId).IsUnique();

                r.HasOne(x => x.Farmer).WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.NoAction);
                r.HasOne(x => x.Equipment).WithMany(e => e.Reviews).HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.NoAction);
                r.HasOne(x => x.Godown).WithMany(g => g.Reviews).HasForeignKey(x => x.GodownId).OnDelete(DeleteBehavior.NoAction);
                r.HasOne(x => x.EquipmentBooking).WithOne(b => b.Review).HasForeignKey<Review>(x => x.EquipmentBookingId).OnDelete(DeleteBehavior.NoAction);
                r.HasOne(x => x.GodownBooking).WithOne(b => b.Review).HasForeignKey<Review>(x => x.GodownBookingId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Notification>(n =>
            {
                n.Property(x => x.Title).HasMaxLength(150);
                n.Property(x => x.Message).HasMaxLength(1000);
                n.Property(x => x.LinkUrl).HasMaxLength(255);
                n.Property(x => x.Type).HasMaxLength(50);
                n.Property(x => x.DedupeKey).HasMaxLength(120);
                n.Property(x => x.TitleKey).HasMaxLength(100);
                n.Property(x => x.MessageKey).HasMaxLength(100);
                n.Property(x => x.ArgsJson).HasMaxLength(500);

                n.HasIndex(x => new { x.UserId, x.IsRead });
                n.HasIndex(x => new { x.UserId, x.CreatedAt });
                n.HasIndex(x => new { x.UserId, x.DedupeKey })
                    .IsUnique()
                    .HasFilter("[DedupeKey] IS NOT NULL");

                n.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<OwnerVerificationRequest>(v =>
            {
                v.Property(x => x.NidNumber).HasMaxLength(500);
                v.Property(x => x.NidLast4).HasMaxLength(10);
                v.Property(x => x.NidFrontImagePath).HasMaxLength(255);
                v.Property(x => x.NidBackImagePath).HasMaxLength(255);
                v.Property(x => x.TradeLicenseImagePath).HasMaxLength(255);
                v.Property(x => x.Status).HasMaxLength(30).HasDefaultValue("Pending");
                v.Property(x => x.RejectionReason).HasMaxLength(500);
                v.Property(x => x.AdminNotes).HasMaxLength(500);
                v.Property(x => x.ReviewedByAdminId).HasMaxLength(450);

                v.HasIndex(x => new { x.UserId, x.Status });
                v.HasIndex(x => x.SubmittedAt);
                v.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<EquipmentMaintenanceRecord>(m =>
            {
                m.Property(x => x.ServiceType).HasMaxLength(80);
                m.Property(x => x.Description).HasMaxLength(1000);
                m.Property(x => x.ServicedBy).HasMaxLength(150);
                m.Property(x => x.Cost).HasPrecision(18, 2);

                m.HasIndex(x => x.EquipmentId);
                m.HasIndex(x => new { x.EquipmentId, x.ServiceDate });
                m.HasOne(x => x.Equipment)
                    .WithMany(e => e.MaintenanceRecords)
                    .HasForeignKey(x => x.EquipmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<LoyaltyPointTransaction>(l =>
            {
                l.Property(x => x.Type).HasMaxLength(30);
                l.Property(x => x.Description).HasMaxLength(500);
                l.Property(x => x.BookingType).HasMaxLength(20);
                l.Property(x => x.BookingCode).HasMaxLength(30);
                l.Property(x => x.PromoCode).HasMaxLength(50);
                l.Property(x => x.DiscountAmount).HasPrecision(18, 2);
                l.Property(x => x.AmountSpent).HasPrecision(18, 2);

                l.HasIndex(x => x.UserId);
                l.HasIndex(x => new { x.UserId, x.CreatedAt });
                l.HasIndex(x => x.PromoCode);
                l.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CropCalendarEntry>(c =>
            {
                c.Property(x => x.Name).HasMaxLength(100).IsRequired();
                c.Property(x => x.BanglaName).HasMaxLength(100).IsRequired();
                c.Property(x => x.ScientificName).HasMaxLength(100);
                c.Property(x => x.Category).HasMaxLength(50);
                c.Property(x => x.Season).HasMaxLength(50);
                c.Property(x => x.DurationDays).HasMaxLength(50);
                c.Property(x => x.OptimalTemperature).HasMaxLength(50);
                c.Property(x => x.SoilTypes).HasMaxLength(150);
                c.Property(x => x.WaterRequirement).HasMaxLength(255);
                c.Property(x => x.PopularVarieties).HasMaxLength(300);
                c.Property(x => x.MajorDistricts).HasMaxLength(300);
                c.Property(x => x.Division).HasMaxLength(100);
                c.Property(x => x.KeyTips).HasMaxLength(1000);
                c.Property(x => x.IconClass).HasMaxLength(50);
                c.Property(x => x.BadgeColor).HasMaxLength(30);
                c.Property(x => x.ProfileCropName).HasMaxLength(60);

                var intListComparer = new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<int>>(
                    (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList());

                c.Property(x => x.SowingMonths)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => string.IsNullOrEmpty(v) ? new List<int>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                    )
                    .Metadata.SetValueComparer(intListComparer);

                c.Property(x => x.GrowingMonths)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => string.IsNullOrEmpty(v) ? new List<int>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                    )
                    .Metadata.SetValueComparer(intListComparer);

                c.Property(x => x.HarvestingMonths)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => string.IsNullOrEmpty(v) ? new List<int>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                    )
                    .Metadata.SetValueComparer(intListComparer);

                c.HasIndex(x => x.Category);
                c.HasIndex(x => x.Season);
                c.HasIndex(x => x.ProfileCropName);
            });
        }
    }
}
