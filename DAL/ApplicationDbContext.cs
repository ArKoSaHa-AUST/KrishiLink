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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(u =>
            {
                u.Property(x => x.District).HasMaxLength(60);
                u.Property(x => x.Specialization).HasMaxLength(60);
            });

            builder.Entity<Equipment>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100);
                e.Property(x => x.Category).HasMaxLength(50);
                e.Property(x => x.Location).HasMaxLength(150);
                e.Property(x => x.DailyRate).HasPrecision(18, 2);
                e.Property(x => x.HourlyRate).HasPrecision(18, 2);
                e.HasIndex(x => x.OwnerId);
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
                g.HasIndex(x => x.OwnerId);
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
        }
    }
}
