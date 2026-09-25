using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using KrishiLink.Models.Entities;

namespace KrishiLink.DAL
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        /// <summary>Shadow concurrency token, mapped by Npgsql to the xmin system column.</summary>
        public const string RowVersionProperty = "xmin";

        /// <summary>Advisory locks held by the active outermost <see cref="Repositories.WorkflowTransaction"/>, if any.</summary>
        internal Repositories.HeldWorkflowLocks? HeldWorkflowLocks { get; set; }

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
        public DbSet<SavedCropAdvisory> SavedCropAdvisories { get; set; } = null!;
        public DbSet<SuggestionState> SuggestionStates { get; set; } = null!;
        public DbSet<PestAlertHistory> PestAlertHistories { get; set; } = null!;
        public DbSet<PestAlertFeedback> PestAlertFeedback { get; set; } = null!;
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
        public DbSet<HarvestPlan> HarvestPlans { get; set; } = null!;
        public DbSet<HarvestPlanItem> HarvestPlanItems { get; set; } = null!;
        public DbSet<SeasonCost> SeasonCosts { get; set; } = null!;
        public DbSet<EmailDeliveryLog> EmailDeliveryLogs { get; set; } = null!;
        public DbSet<Favorite> Favorites { get; set; } = null!;
        public DbSet<SavedSearch> SavedSearches { get; set; } = null!;
        public DbSet<StorageIntakeLot> StorageIntakeLots { get; set; } = null!;
        public DbSet<SupabaseSessionRecord> SupabaseSessions { get; set; } = null!;
        public DbSet<AgentConversation> AgentConversations { get; set; } = null!;
        public DbSet<AgentMessage> AgentMessages { get; set; } = null!;
        public DbSet<CommunityPost> CommunityPosts { get; set; } = null!;
        public DbSet<PostMedia> PostMedia { get; set; } = null!;
        public DbSet<CommunityComment> CommunityComments { get; set; } = null!;
        public DbSet<CommunityReaction> CommunityReactions { get; set; } = null!;
        public DbSet<CommunityBookmark> CommunityBookmarks { get; set; } = null!;
        public DbSet<CommunityPostReport> CommunityPostReports { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasDefaultSchema(DatabaseConfiguration.Schema);
            // Trigram indexes make ILIKE and typo-tolerant search index-backed (DIS-01).
            builder.HasPostgresExtension("pg_trgm");

            builder.Entity<ApplicationUser>(u =>
            {
                u.Property(x => x.District).HasMaxLength(60);
                u.Property(x => x.Specialization).HasMaxLength(60);
                u.Property(x => x.PreferredLandUnit).HasConversion<string>().HasMaxLength(10);
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
                u.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("\"PhoneNumber\" IS NOT NULL");
            });

            builder.Entity<Equipment>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100);
                e.HasGeneratedTsVectorColumn(x => x.SearchVector, "simple", x => new { x.Name, x.Category, x.Description, x.District })
                    .HasIndex(x => x.SearchVector).HasMethod("GIN");
                e.HasIndex(x => x.Name).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("IX_Equipment_Name_trgm");
                e.HasIndex(x => x.Description).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("IX_Equipment_Description_trgm");
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
                b.Property(x => x.ModificationCount).HasDefaultValue(0);
                b.Property(x => x.PreviousDetails).HasMaxLength(200);
                b.Property(x => x.VerifyToken).HasMaxLength(24);
                b.HasIndex(x => new { x.EquipmentId, x.Status });
                b.HasOne(x => x.Equipment).WithMany(x => x.Bookings).HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(x => x.Farmer).WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(x => x.Payout).WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.HarvestPlan).WithMany().HasForeignKey(x => x.HarvestPlanId).OnDelete(DeleteBehavior.SetNull);
                b.HasIndex(x => x.HarvestPlanId);
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
                g.HasGeneratedTsVectorColumn(x => x.SearchVector, "simple", x => new { x.Name, x.StorageType, x.Description, x.District, x.Facilities })
                    .HasIndex(x => x.SearchVector).HasMethod("GIN");
                g.HasIndex(x => x.Name).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("IX_Godowns_Name_trgm");
                g.HasIndex(x => x.Description).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("IX_Godowns_Description_trgm");
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
                b.Property(x => x.ModificationCount).HasDefaultValue(0);
                b.Property(x => x.PreviousDetails).HasMaxLength(200);
                b.Property(x => x.VerifyToken).HasMaxLength(24);
                b.HasIndex(x => new { x.GodownId, x.Status });
                b.HasOne(x => x.Godown).WithMany(x => x.Bookings).HasForeignKey(x => x.GodownId).OnDelete(DeleteBehavior.Cascade);
                b.HasOne(x => x.Farmer).WithMany().HasForeignKey(x => x.FarmerId).OnDelete(DeleteBehavior.Restrict);
                b.HasOne(x => x.Payout).WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
                b.HasOne(x => x.HarvestPlan).WithMany().HasForeignKey(x => x.HarvestPlanId).OnDelete(DeleteBehavior.SetNull);
                b.HasIndex(x => x.HarvestPlanId);
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
                x.Property(e => e.Category).HasMaxLength(30).HasDefaultValue(ExpenseCategories.Other);
                x.Property(e => e.Note).HasMaxLength(200);
                x.Property(e => e.Amount).HasPrecision(18, 2);
                x.Property(e => e.ExpenseDate).HasDefaultValueSql("now()");
                x.HasIndex(e => new { e.OwnerId, e.BookingType, e.ExpenseDate });
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
                    .HasFilter("\"DedupeKey\" IS NOT NULL");

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
                v.HasIndex(x => x.UserId).IsUnique().HasFilter("\"Status\" = 'Pending'");
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

                c.Property(x => x.Key).HasMaxLength(60).HasDefaultValue(string.Empty);
                c.Property(x => x.SoilTypesBn).HasMaxLength(200);
                c.Property(x => x.WaterRequirementBn).HasMaxLength(300);
                c.Property(x => x.KeyTipsBn).HasMaxLength(1000);
                // No database default: Low is the CLR default, so EF would omit it on insert and the default would silently win.
                c.Property(x => x.WaterNeed).HasConversion<string>().HasMaxLength(10);
                c.Property(x => x.Source).HasMaxLength(40).HasDefaultValue(string.Empty);

                c.HasIndex(x => x.Category);
                c.HasIndex(x => x.Season);
                c.HasIndex(x => x.ProfileCropName);
                c.HasIndex(x => x.Key).IsUnique().HasFilter("\"Key\" <> ''");
            });

            builder.Entity<SavedCropAdvisory>(s =>
            {
                s.Property(x => x.UserId).HasMaxLength(450).IsRequired();
                s.Property(x => x.Season).HasMaxLength(20).IsRequired();
                s.Property(x => x.SoilType).HasMaxLength(40).IsRequired();
                s.Property(x => x.District).HasMaxLength(60).IsRequired();
                s.Property(x => x.FactorsJson).IsRequired();
                s.HasIndex(x => new { x.UserId, x.CreatedAt });
                s.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                s.HasOne(x => x.Crop).WithMany().HasForeignKey(x => x.CropCalendarEntryId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PestAlertHistory>(h =>
            {
                h.Property(x => x.District).HasMaxLength(60).IsRequired();
                h.Property(x => x.Severity).HasMaxLength(20).IsRequired();
                h.Property(x => x.WeatherSnapshotJson).IsRequired();
                h.HasIndex(x => new { x.District, x.RuleId, x.TriggeredOn }).IsUnique();
                h.HasIndex(x => x.TriggeredOn);
            });

            builder.Entity<PestAlertFeedback>(fb =>
            {
                fb.Property(x => x.UserId).HasMaxLength(450).IsRequired();
                fb.HasIndex(x => new { x.PestAlertHistoryId, x.UserId }).IsUnique();
                fb.HasOne(x => x.History).WithMany(h => h.Feedback).HasForeignKey(x => x.PestAlertHistoryId).OnDelete(DeleteBehavior.Cascade);
                fb.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SuggestionState>(s =>
            {
                s.Property(x => x.UserId).HasMaxLength(450).IsRequired();
                s.Property(x => x.SuggestionKey).HasMaxLength(160).IsRequired();
                s.Property(x => x.State).HasConversion<string>().HasMaxLength(10);
                s.HasIndex(x => new { x.UserId, x.SuggestionKey }).IsUnique();
                s.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<HarvestPlan>(p =>
            {
                p.Property(x => x.Name).HasMaxLength(80).IsRequired();
                p.Property(x => x.Crop).HasMaxLength(60);
                p.Property(x => x.Note).HasMaxLength(500);
                p.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(HarvestPlanStatus.Draft);
                p.Property(x => x.ExpectedPricePerKg).HasPrecision(18, 2);
                p.HasOne(x => x.CropEntry).WithMany().HasForeignKey(x => x.CropCalendarEntryId).OnDelete(DeleteBehavior.SetNull);

                p.HasMany(x => x.Costs)
                    .WithOne(x => x.Plan)
                    .HasForeignKey(x => x.HarvestPlanId)
                    .OnDelete(DeleteBehavior.Cascade);

                p.HasOne(x => x.Farmer)
                    .WithMany()
                    .HasForeignKey(x => x.FarmerId)
                    .OnDelete(DeleteBehavior.Restrict);

                p.HasMany(x => x.Items)
                    .WithOne(x => x.Plan)
                    .HasForeignKey(x => x.HarvestPlanId)
                    .OnDelete(DeleteBehavior.Cascade);

                p.HasIndex(x => new { x.FarmerId, x.Status });
            });

            builder.Entity<EmailDeliveryLog>(l =>
            {
                l.Property(x => x.RecipientHash).HasMaxLength(24).IsRequired();
                l.Property(x => x.UserId).HasMaxLength(450);
                l.Property(x => x.Subject).HasMaxLength(200).IsRequired();
                l.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                l.Property(x => x.Error).HasMaxLength(300);
                // Support looks a user up by address fingerprint or account, newest first; the sweep deletes by age.
                l.HasIndex(x => new { x.RecipientHash, x.AttemptedAt });
                l.HasIndex(x => new { x.UserId, x.AttemptedAt });
                l.HasIndex(x => x.AttemptedAt);
            });

            builder.Entity<SeasonCost>(c =>
            {
                c.Property(x => x.Category).HasMaxLength(20).IsRequired();
                c.Property(x => x.Note).HasMaxLength(200).IsRequired();
                c.Property(x => x.Amount).HasPrecision(18, 2);
                c.HasIndex(x => x.HarvestPlanId);
            });

            builder.Entity<HarvestPlanItem>(i =>
            {
                i.Property(x => x.ItemType).HasMaxLength(20).IsRequired();
                i.Property(x => x.Note).HasMaxLength(300);

                i.HasIndex(x => x.HarvestPlanId);
            });

            builder.Entity<Favorite>(f =>
            {
                f.Property(x => x.ListingType).HasMaxLength(20).IsRequired();
                f.HasIndex(x => new { x.UserId, x.ListingType, x.ListingId }).IsUnique();
                f.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SavedSearch>(s =>
            {
                s.Property(x => x.Name).HasMaxLength(80).IsRequired();
                s.Property(x => x.ListingType).HasMaxLength(20).IsRequired();
                s.Property(x => x.SearchTerm).HasMaxLength(100);
                s.Property(x => x.Category).HasMaxLength(50);
                s.Property(x => x.District).HasMaxLength(60);
                s.Property(x => x.MaxRate).HasPrecision(18, 2);
                s.Property(x => x.KnownListingIds).HasMaxLength(2000).HasDefaultValue("");

                s.HasIndex(x => x.UserId);
                s.HasIndex(x => x.AlertsEnabled);

                s.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<StorageIntakeLot>(lot =>
            {
                lot.Property(x => x.ReceiptNumber).HasMaxLength(20).IsRequired();
                lot.Property(x => x.Crop).HasMaxLength(60).IsRequired();
                lot.Property(x => x.Variety).HasMaxLength(60);
                lot.Property(x => x.BagWeightKg).HasPrecision(6, 2);
                lot.Property(x => x.NetWeightKg).HasPrecision(12, 2);
                lot.Property(x => x.MoisturePercent).HasPrecision(5, 2);
                lot.Property(x => x.Grade).HasMaxLength(10).HasDefaultValue(IntakeGrades.Ungraded);
                lot.Property(x => x.Remarks).HasMaxLength(500);
                lot.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(IntakeLotStatus.Stored);
                lot.Property(x => x.ReleasedTo).HasMaxLength(120);
                lot.Property(x => x.ReleaseRemarks).HasMaxLength(300);
                lot.Property(x => x.RecordedByUserId).HasMaxLength(450);

                lot.HasIndex(x => x.ReceiptNumber).IsUnique();
                lot.HasIndex(x => x.GodownBookingId);
                lot.HasOne(x => x.Booking)
                    .WithMany(b => b.IntakeLots)
                    .HasForeignKey(x => x.GodownBookingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<SupabaseSessionRecord>(s =>
            {
                s.ToTable("SupabaseSessions");
                s.HasKey(x => x.Id);
                s.Property(x => x.Id).HasMaxLength(64);
                s.Property(x => x.UserId).HasMaxLength(450).IsRequired();
                s.Property(x => x.SecurityStamp).HasMaxLength(256).IsRequired();
                s.Property(x => x.ProtectedTokens).IsRequired();
                s.HasIndex(x => x.UserId);
                s.HasIndex(x => x.ExpiresAt);
                s.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AgentConversation>(c =>
            {
                c.Property(x => x.UserId).HasMaxLength(450).IsRequired();
                c.Property(x => x.Title).HasMaxLength(120).IsRequired();
                c.HasIndex(x => new { x.UserId, x.UpdatedAt }).IsDescending(false, true);
                c.HasIndex(x => x.UpdatedAt);
                c.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AgentMessage>(m =>
            {
                m.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
                m.Property(x => x.Content).IsRequired();
                m.Property(x => x.ToolName).HasMaxLength(64);
                m.HasIndex(x => new { x.ConversationId, x.CreatedAt });
                m.HasIndex(x => x.ProposalId).IsUnique().HasFilter("\"ProposalId\" IS NOT NULL");
                m.HasOne(x => x.Conversation).WithMany(c => c.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CommunityPost>(p =>
            {
                p.Property(x => x.Content).IsRequired();
                p.Property(x => x.PostType).HasMaxLength(30).IsRequired().HasDefaultValue(CommunityPostTypes.Experience);
                p.Property(x => x.CropCategory).HasMaxLength(60);
                p.Property(x => x.IssueCategory).HasMaxLength(60);
                p.Property(x => x.UrgencyLevel).HasMaxLength(20).HasDefaultValue(CommunityUrgencyLevels.Normal);
                p.Property(x => x.CropAge).HasMaxLength(40);
                p.Property(x => x.AffectedArea).HasMaxLength(40);
                p.Property(x => x.District).HasMaxLength(60);
                p.Property(x => x.Upazila).HasMaxLength(60);
                p.Property(x => x.AudioRecordingUrl).HasMaxLength(255);

                p.HasIndex(x => x.AuthorId);
                p.HasIndex(x => x.PostType);
                p.HasIndex(x => x.IsHelpRequest);
                p.HasIndex(x => x.IsSolved);
                p.HasIndex(x => x.UrgencyLevel);
                p.HasIndex(x => x.District);
                p.HasIndex(x => x.CreatedAt);

                p.HasOne(x => x.Author)
                    .WithMany()
                    .HasForeignKey(x => x.AuthorId)
                    .OnDelete(DeleteBehavior.Restrict);

                p.HasOne(x => x.AcceptedComment)
                    .WithMany()
                    .HasForeignKey(x => x.AcceptedCommentId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<PostMedia>(m =>
            {
                m.Property(x => x.MediaUrl).HasMaxLength(255).IsRequired();
                m.Property(x => x.MediaType).HasMaxLength(20).HasDefaultValue(PostMediaTypes.Image);
                m.Property(x => x.ThumbnailUrl).HasMaxLength(255);

                m.HasIndex(x => x.PostId);
                m.HasOne(x => x.Post)
                    .WithMany(p => p.MediaList)
                    .HasForeignKey(x => x.PostId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CommunityComment>(c =>
            {
                c.Property(x => x.Content).IsRequired();
                c.Property(x => x.AttachmentImageUrl).HasMaxLength(255);
                c.Property(x => x.AudioRecordingUrl).HasMaxLength(255);

                c.HasIndex(x => x.PostId);
                c.HasIndex(x => x.AuthorId);
                c.HasIndex(x => x.ParentCommentId);
                c.HasIndex(x => x.CreatedAt);

                c.HasOne(x => x.Post)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(x => x.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

                c.HasOne(x => x.Author)
                    .WithMany()
                    .HasForeignKey(x => x.AuthorId)
                    .OnDelete(DeleteBehavior.Restrict);

                c.HasOne(x => x.ParentComment)
                    .WithMany(c => c.Replies)
                    .HasForeignKey(x => x.ParentCommentId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<CommunityReaction>(r =>
            {
                r.Property(x => x.ReactionType).HasMaxLength(20).HasDefaultValue(CommunityReactionTypes.Helpful);

                r.HasIndex(x => new { x.UserId, x.PostId, x.CommentId }).IsUnique();
                r.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                r.HasOne(x => x.Post)
                    .WithMany(p => p.Reactions)
                    .HasForeignKey(x => x.PostId)
                    .OnDelete(DeleteBehavior.Cascade);

                r.HasOne(x => x.Comment)
                    .WithMany(c => c.Reactions)
                    .HasForeignKey(x => x.CommentId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<CommunityBookmark>(b =>
            {
                b.HasIndex(x => new { x.UserId, x.PostId }).IsUnique();
                b.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(x => x.Post)
                    .WithMany(p => p.Bookmarks)
                    .HasForeignKey(x => x.PostId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CommunityPostReport>(rep =>
            {
                rep.Property(x => x.Reason).HasMaxLength(300).IsRequired();
                rep.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Pending");

                rep.HasIndex(x => x.PostId);
                rep.HasIndex(x => x.Status);

                rep.HasOne(x => x.Reporter)
                    .WithMany()
                    .HasForeignKey(x => x.ReporterId)
                    .OnDelete(DeleteBehavior.Restrict);

                rep.HasOne(x => x.Post)
                    .WithMany(p => p.Reports)
                    .HasForeignKey(x => x.PostId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Optimistic concurrency on PostgreSQL's xmin system column: a write based on a stale read fails with
            // DbUpdateConcurrencyException instead of silently overwriting. No column is added to the tables.
            foreach (var concurrent in new[] { typeof(EquipmentBooking), typeof(GodownBooking), typeof(Equipment), typeof(Godown), typeof(Payment), typeof(Transaction), typeof(HarvestPlan) })
                builder.Entity(concurrent).Property<uint>(RowVersionProperty).IsRowVersion();

            // Calendar dates are not instants: they must not shift with a server's timezone.
            var calendarDates = new HashSet<string>
            {
                "Date", "StartDate", "EndDate", "ServiceDate", "ForecastDate", "LastStatementSentMonth", "IntakeDate", "TriggeredOn", "IncurredOn"
            };
            var utcConverter = new UtcDateTimeConverter();
            var calendarConverter = new CalendarDateTimeConverter();
            foreach (var entity in builder.Model.GetEntityTypes())
                foreach (var property in entity.GetProperties())
                {
                    if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) != typeof(DateTime))
                        continue;
                    var isCalendarDate = calendarDates.Contains(property.Name);
                    property.SetColumnType(isCalendarDate ? "date" : "timestamp with time zone");
                    property.SetValueConverter(isCalendarDate ? calendarConverter : utcConverter);
                }
        }
    }
}
