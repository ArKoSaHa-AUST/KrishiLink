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
                u.HasIndex(x => x.IsVerified);
                u.HasIndex(x => x.VerificationStatus);
            });

            builder.Entity<Equipment>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100);
                e.Property(x => x.Category).HasMaxLength(50);
                e.Property(x => x.Location).HasMaxLength(150);
                e.Property(x => x.DailyRate).HasPrecision(18, 2);
                e.Property(x => x.HourlyRate).HasPrecision(18, 2);
                e.Property(x => x.AverageRating).HasDefaultValue(0.0);
                e.Property(x => x.ReviewCount).HasDefaultValue(0);
                e.HasIndex(x => x.OwnerId);
                e.HasIndex(x => new { x.Latitude, x.Longitude });
                e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<EquipmentBooking>(b =>
            {
                b.Property(x => x.Status).HasMaxLength(20);
                b.HasIndex(x => new { x.EquipmentId, x.Status });
                b.HasOne(x => x.Equipment).WithMany(x => x.Bookings).HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(x => x.Farmer).WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(x => x.Payout).WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<EquipmentBlockedDate>(d =>
            {
                d.HasIndex(x => new { x.EquipmentId, x.Date }).IsUnique();
                d.HasOne(x => x.Equipment).WithMany(x => x.BlockedDates).HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Godown>(g =>
            {
                g.Property(x => x.Name).HasMaxLength(120);
                g.Property(x => x.StorageType).HasMaxLength(50);
                g.Property(x => x.Location).HasMaxLength(150);
                g.Property(x => x.PricePerTonPerMonth).HasPrecision(18, 2);
                g.Property(x => x.AverageRating).HasDefaultValue(0.0);
                g.Property(x => x.ReviewCount).HasDefaultValue(0);
                g.HasIndex(x => x.OwnerId);
                g.HasIndex(x => new { x.Latitude, x.Longitude });
                g.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<GodownBooking>(b =>
            {
                b.Property(x => x.Status).HasMaxLength(20);
                b.HasIndex(x => new { x.GodownId, x.Status });
                b.HasOne(x => x.Godown).WithMany(x => x.Bookings).HasForeignKey(x => x.GodownId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(x => x.Farmer).WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(x => x.Payout).WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<GodownBlockedDate>(d =>
            {
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
                t.Property(x => x.PaymentMethod).HasMaxLength(30);
                t.Property(x => x.PayoutAccount).HasMaxLength(40);
                t.Property(x => x.Status).HasMaxLength(20);
                t.Property(x => x.GrossAmount).HasPrecision(18, 2);
                t.Property(x => x.Commission).HasPrecision(18, 2);
                t.Property(x => x.Amount).HasPrecision(18, 2);
                t.HasIndex(x => x.UserId);
                t.HasIndex(x => x.Reference).IsUnique();
            });

            builder.Entity<Review>(r =>
            {
                r.Property(x => x.Comment).HasMaxLength(1000);
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
                n.HasIndex(x => new { x.UserId, x.IsRead });
                n.HasIndex(x => new { x.UserId, x.CreatedAt });

                n.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<OwnerVerificationRequest>(v =>
            {
                v.Property(x => x.NidNumber).HasMaxLength(30);
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
        }
    }
}
