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
        public DbSet<Godown> Godowns { get; set; } = null!;
        public DbSet<GodownBooking> GodownBookings { get; set; } = null!;
        public DbSet<Crop> Crops { get; set; } = null!;
        public DbSet<CropRecommendation> CropRecommendations { get; set; } = null!;
        public DbSet<WeatherData> WeatherData { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Equipment>()
                .Property(e => e.DailyRate)
                .HasPrecision(18, 2);

            builder.Entity<Godown>()
                .Property(g => g.PricePerTonPerMonth)
                .HasPrecision(18, 2);

            builder.Entity<Transaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);
        }
    }
}
