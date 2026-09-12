using KrishiLink.BLL.Helpers;
using KrishiLink.BLL.Services;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KrishiLink.DAL
{
    /// <summary>
    /// Applies pending migrations and guarantees the Identity roles exist. In Development it also loads a
    /// small, realistic demo dataset (one account per role) when the database has no listings yet.
    /// </summary>
    public static class DbInitializer
    {
        public const string DemoCredential = "Krishi@123";

        public static async Task InitializeAsync(IServiceProvider services, bool seedDemoData)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in AppRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            if (seedDemoData && !await db.Equipment.AnyAsync() && !await db.Godowns.AnyAsync())
                await SeedDemoDataAsync(db, services.GetRequiredService<UserManager<ApplicationUser>>());
            else if (seedDemoData && !await db.Reviews.AnyAsync())
                await SeedDemoReviewsAsync(db);

            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));
            var commissionRate = services.GetRequiredService<IOptions<RevenueOptions>>().Value.PlatformCommissionRate;
            var backfilled = await BackfillPriceSnapshotsAsync(db, commissionRate);
            if (backfilled > 0) logger.LogInformation("Backfilled price snapshots on {Count} legacy booking(s).", backfilled);

            var conservation = services.GetRequiredService<ILedgerService>().CheckConservation();
            if (conservation.Ok)
                logger.LogInformation("Ledger conservation OK: {Detail}", conservation.Detail);
            else
            {
                logger.LogWarning("Ledger conservation FAILED (lhs={Lhs}, rhs={Rhs}): {Detail}", conservation.Lhs, conservation.Rhs, conservation.Detail);
                if (seedDemoData) throw new InvalidOperationException($"Seeded ledger does not conserve money: {conservation.Detail}");
            }

            // Idempotent migration of legacy verification uploads from wwwroot to App_Data
            var env = services.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            if (env != null)
            {
                var legacyDir = Path.Combine(env.WebRootPath, "uploads", "verifications");
                if (Directory.Exists(legacyDir))
                {
                    var usersWithLegacy = await db.Users
                        .Where(u => (u.NidFrontImagePath != null && u.NidFrontImagePath.Contains("uploads/verifications")) ||
                                    (u.NidBackImagePath != null && u.NidBackImagePath.Contains("uploads/verifications")) ||
                                    (u.TradeLicenseImagePath != null && u.TradeLicenseImagePath.Contains("uploads/verifications")))
                        .ToListAsync();

                    foreach (var u in usersWithLegacy)
                    {
                        u.NidFrontImagePath = MoveLegacyVerificationFile(u.NidFrontImagePath, u.Id, env.WebRootPath, env.ContentRootPath);
                        u.NidBackImagePath = MoveLegacyVerificationFile(u.NidBackImagePath, u.Id, env.WebRootPath, env.ContentRootPath);
                        u.TradeLicenseImagePath = MoveLegacyVerificationFile(u.TradeLicenseImagePath, u.Id, env.WebRootPath, env.ContentRootPath);
                    }

                    var requestsWithLegacy = await db.VerificationRequests
                        .Where(r => (r.NidFrontImagePath != null && r.NidFrontImagePath.Contains("uploads/verifications")) ||
                                    (r.NidBackImagePath != null && r.NidBackImagePath.Contains("uploads/verifications")) ||
                                    (r.TradeLicenseImagePath != null && r.TradeLicenseImagePath.Contains("uploads/verifications")))
                        .ToListAsync();

                    foreach (var r in requestsWithLegacy)
                    {
                        r.NidFrontImagePath = MoveLegacyVerificationFile(r.NidFrontImagePath, r.UserId, env.WebRootPath, env.ContentRootPath) ?? r.NidFrontImagePath;
                        r.NidBackImagePath = MoveLegacyVerificationFile(r.NidBackImagePath, r.UserId, env.WebRootPath, env.ContentRootPath);
                        r.TradeLicenseImagePath = MoveLegacyVerificationFile(r.TradeLicenseImagePath, r.UserId, env.WebRootPath, env.ContentRootPath);
                    }

                    await db.SaveChangesAsync();

                    try
                    {
                        if (Directory.GetFiles(legacyDir, "*", SearchOption.AllDirectories).Length == 0)
                        {
                            Directory.Delete(legacyDir, true);
                        }
                    }
                    catch { }
                }
            }

            // Ensure demo owner accounts have verified badges and admin user exists
            if (seedDemoData)
            {
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                var admin = await userManager.FindByEmailAsync("admin@krishilink.com");
                if (admin == null)
                {
                    await CreateUserAsync(userManager, "admin@krishilink.com", "01719000001", "Admin Karim", AppRoles.Admin, "Dhaka Sadar, Dhaka", "Administration", "KrishiLink Operations");
                }

                var demoEq = await userManager.FindByEmailAsync("equipment@krishilink.com");
                if (demoEq != null && !demoEq.IsVerified)
                {
                    demoEq.IsVerified = true;
                    demoEq.VerificationStatus = "Verified";
                    demoEq.NidNumber = "19882692012345";
                    demoEq.VerificationReviewedAt = DateTime.UtcNow.AddMonths(-6);
                    demoEq.VerificationNotes = "Verified owner account.";
                    await userManager.UpdateAsync(demoEq);
                }

                var demoGd = await userManager.FindByEmailAsync("godown@krishilink.com");
                if (demoGd != null && !demoGd.IsVerified)
                {
                    demoGd.IsVerified = true;
                    demoGd.VerificationStatus = "Verified";
                    demoGd.NidNumber = "19752718098765";
                    demoGd.VerificationReviewedAt = DateTime.UtcNow.AddMonths(-6);
                    demoGd.VerificationNotes = "Verified owner account.";
                    await userManager.UpdateAsync(demoGd);
                }

                // Backfill map coordinates and district for any listings that have null values
                var unmappedEquipments = await db.Equipment.Where(e => e.Latitude == null || e.Longitude == null || e.District == null).ToListAsync();
                if (unmappedEquipments.Any())
                {
                    foreach (var eq in unmappedEquipments)
                    {
                        if (eq.Latitude == null || eq.Longitude == null)
                        {
                            var (lat, lng) = GeoLocationHelper.GetDistrictCoordinates(eq.Location);
                            eq.Latitude = lat;
                            eq.Longitude = lng;
                        }
                        if (eq.District == null)
                        {
                            eq.District = OnboardingOptions.GuessDistrict(eq.Location);
                        }
                    }
                    await db.SaveChangesAsync();
                }

                var unmappedGodowns = await db.Godowns.Where(g => g.Latitude == null || g.Longitude == null || g.District == null).ToListAsync();
                if (unmappedGodowns.Any())
                {
                    foreach (var gd in unmappedGodowns)
                    {
                        if (gd.Latitude == null || gd.Longitude == null)
                        {
                            var (lat, lng) = GeoLocationHelper.GetDistrictCoordinates(gd.Location);
                            gd.Latitude = lat;
                            gd.Longitude = lng;
                        }
                        if (gd.District == null)
                        {
                            gd.District = OnboardingOptions.GuessDistrict(gd.Location);
                        }
                    }
                    await db.SaveChangesAsync();
                }

                // Seed demo loyalty points for demo farmers
                await SeedDemoLoyaltyPointsAsync(db, userManager);

                // Seed DAE crop calendar entries
                await SeedCropCalendarAsync(db);

                // Seed demo equipment rate rules and min rental days
                await SeedDemoRateRulesAsync(db);
            }
        }

        private static async Task SeedDemoRateRulesAsync(ApplicationDbContext db)
        {
            if (!await db.EquipmentRateRules.AnyAsync())
            {
                var tractor = await db.Equipment.FirstOrDefaultAsync(e => e.Name.Contains("Mahindra 575 DI"));
                if (tractor != null)
                {
                    tractor.MinRentalDays = 2;
                    var currentYear = DateTime.Today.Year;
                    db.EquipmentRateRules.AddRange(
                        new EquipmentRateRule
                        {
                            EquipmentId = tractor.Id,
                            Kind = "Season",
                            Name = "Boro Harvest Peak",
                            StartDate = new DateTime(currentYear, 4, 15),
                            EndDate = new DateTime(currentYear, 5, 31),
                            DailyRate = 2000m,
                            IsActive = true
                        },
                        new EquipmentRateRule
                        {
                            EquipmentId = tractor.Id,
                            Kind = "Weekend",
                            Name = "Weekend Rate",
                            DailyRate = 1800m,
                            IsActive = true
                        }
                    );
                    await db.SaveChangesAsync();
                }
            }
        }

        private static string? MoveLegacyVerificationFile(string? path, string userId, string webRootPath, string contentRootPath)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            var clean = path.TrimStart('~', '/');
            if (!clean.StartsWith("uploads/verifications", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            var oldFullPath = Path.Combine(webRootPath, clean.Replace('/', Path.DirectorySeparatorChar));
            var fileName = Path.GetFileName(clean);
            var targetFolder = Path.Combine(contentRootPath, "App_Data", "verifications", userId);
            Directory.CreateDirectory(targetFolder);

            var newFullPath = Path.Combine(targetFolder, fileName);
            if (File.Exists(oldFullPath))
            {
                try
                {
                    if (!File.Exists(newFullPath))
                    {
                        File.Move(oldFullPath, newFullPath);
                    }
                    else
                    {
                        File.Delete(oldFullPath);
                    }
                }
                catch { }
            }

            return $"verifications/{userId}/{fileName}";
        }

        private static async Task SeedDemoDataAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            var farmer = await CreateUserAsync(userManager, "farmer@krishilink.com", "01711000001", "Rahim Uddin", AppRoles.Farmer, "Shibganj, Bogra", "Rice (Boro)", "Uddin Agro Farm");
            var karim = await CreateUserAsync(userManager, "karim.mia@krishilink.com", "01711000002", "Karim Mia", AppRoles.Farmer, "Sherpur, Bogra", "Potato");
            var fatema = await CreateUserAsync(userManager, "fatema@krishilink.com", "01711000003", "Fatema Begum", AppRoles.Farmer, "Gabtali, Bogra", "Vegetables");
            var salma = await CreateUserAsync(userManager, "salma@krishilink.com", "01711000004", "Salma Akter", AppRoles.Farmer, "Bochaganj, Dinajpur", "Rice (Aman)");
            var motaleb = await CreateUserAsync(userManager, "motaleb@krishilink.com", "01711000005", "Motaleb Hossain", AppRoles.Farmer, "Birol, Dinajpur", "Wheat");

            var eqOwner = await CreateUserAsync(userManager, "equipment@krishilink.com", "01712000001", "Abdul Karim", AppRoles.EquipmentOwner, "Bogra Sadar, Bogra", "Tractor", "Karim Agro Machinery", isVerified: true, nid: "19882692012345");
            var gdOwner = await CreateUserAsync(userManager, "godown@krishilink.com", "01713000001", "Abdul Mannan", AppRoles.GodownOwner, "Dinajpur Sadar, Dinajpur", "Cold Storage", "Green Grain Storage Ltd.", isVerified: true, nid: "19752718098765");

            var yard = eqOwner.Location!;
            var equipment = new List<Equipment>
            {
                Machine("Mahindra 575 DI Heavy Tractor", "Tractor", 1500, 250, yard,
                    "45 HP diesel tractor with 4WD. Ideal for deep tilling, wet and dry paddy field preparation, rotavator attachments and haulage.",
                    "https://images.unsplash.com/photo-1592878904946-b3cd8ae243d0?auto=format&fit=crop&w=800&q=80", -40),
                Machine("Kubota DC-70 Combine Harvester", "Combine Harvester", 4200, 650, yard,
                    "Track-type combine harvester for paddy and wheat. Cuts, threshes and bags in a single pass; handles wet fields.",
                    "https://images.unsplash.com/photo-1589923188900-85dae523342b?auto=format&fit=crop&w=800&q=80", -35),
                Machine("ACI Power Tiller 12HP", "Power Tiller", 800, 150, yard,
                    "12 HP diesel power tiller with rotary attachment. Suitable for small and medium plots and puddling before transplanting.",
                    "https://images.unsplash.com/photo-1530267981375-f0de937f5f13?auto=format&fit=crop&w=800&q=80", -30),
                Machine("Honda WB30X Irrigation Pump", "Irrigation Pump", 350, 60, yard,
                    "3-inch centrifugal petrol pump, 1,100 L/min. Includes 20 m of delivery hose for shallow tube-well irrigation.",
                    "https://images.unsplash.com/photo-1628352081506-83c43123ed6d?auto=format&fit=crop&w=800&q=80", -25),
                Machine("TAFE 45DI Rotavator", "Power Tiller", 950, 180, yard,
                    "Heavy-duty rotavator for seedbed preparation. Comes with an operator; fuel is charged separately.",
                    "https://images.unsplash.com/photo-1595246140625-573b715d11dc?auto=format&fit=crop&w=800&q=80", -20)
            };
            equipment.ForEach(e => e.OwnerId = eqOwner.Id);
            db.Equipment.AddRange(equipment);

            var godowns = new List<Godown>
            {
                Storage("Green Grain Cold Storage Facility", "Cold Storage", 300, 1200, "Dinajpur Sadar, Dinajpur",
                    "Climate-controlled (2–8°C) cold storage for potato, vegetables and fruit with 24/7 monitoring and backup power.",
                    "Climate Control (2-8°C)|24/7 CCTV|Backup Generator|Moisture Proof",
                    "https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?auto=format&fit=crop&w=800&q=80", -60),
                Storage("Dinajpur AgriHub Warehouse", "Grain Warehouse", 500, 650, "Birganj, Dinajpur",
                    "Fumigated, elevated-platform grain warehouse for paddy and wheat with direct truck access and fire safety.",
                    "Fumigated & Pest-Free|Elevated Platform|Easy Truck Loading|Fire Safety",
                    "https://images.unsplash.com/photo-1595246140625-573b715d11dc?auto=format&fit=crop&w=800&q=80", -50),
                Storage("Riverside Seed Vault", "Seed Vault", 120, 900, "Parbatipur, Dinajpur",
                    "Humidity-controlled seed vault for certified paddy and wheat seed. Separate lots per farmer.",
                    "Humidity Control|Rodent-Proof|Daily Inspection",
                    "https://images.unsplash.com/photo-1578575437130-527eed3abbec?auto=format&fit=crop&w=800&q=80", -45)
            };
            godowns.ForEach(g => g.OwnerId = gdOwner.Id);
            db.Godowns.AddRange(godowns);
            await db.SaveChangesAsync();

            var today = DateTime.Today;
            db.EquipmentBookings.AddRange(
                // History that feeds the revenue report
                Rental(equipment[1], karim, -120, -114, BookingStatus.Completed, -128),
                Rental(equipment[0], fatema, -95, -92, BookingStatus.Completed, -100, "Land preparation before potato season."),
                Rental(equipment[3], motaleb, -70, -55, BookingStatus.Completed, -75, "Boro seedbed irrigation."),
                Rental(equipment[1], farmer, -45, -38, BookingStatus.Completed, -50),
                Rental(equipment[2], farmer, -30, -27, BookingStatus.Completed, -35),
                Rental(equipment[4], salma, -20, -19, BookingStatus.Cancelled, -24),
                Rental(equipment[1], salma, -12, -7, BookingStatus.Rejected, -15, "Harvest window for early Aman.", "Harvester is under scheduled maintenance that week."),
                // Live
                Rental(equipment[0], farmer, -1, 4, BookingStatus.Accepted, -5, "Need standard disc plough attachment for deep tilling."),
                Rental(equipment[4], motaleb, 3, 4, BookingStatus.Accepted, -2),
                Rental(equipment[1], farmer, 6, 10, BookingStatus.Pending, 0, "Need it for 5 acres of Aman paddy harvest."),
                Rental(equipment[2], karim, 9, 11, BookingStatus.Pending, 0),
                Rental(equipment[0], fatema, 14, 16, BookingStatus.Pending, -1, "Land preparation before potato season."));

            db.EquipmentBlockedDates.AddRange(
                new EquipmentBlockedDate { EquipmentId = equipment[0].Id, Date = today.AddDays(8) },
                new EquipmentBlockedDate { EquipmentId = equipment[0].Id, Date = today.AddDays(9) },
                new EquipmentBlockedDate { EquipmentId = equipment[1].Id, Date = today.AddDays(22) });

            db.GodownBlockedDates.AddRange(
                new GodownBlockedDate { GodownId = godowns[2].Id, Date = today.AddDays(12) },
                new GodownBlockedDate { GodownId = godowns[2].Id, Date = today.AddDays(13) });

            db.GodownBookings.AddRange(
                StorageBooking(godowns[1], motaleb, 100, -150, -90, BookingStatus.Completed, -158, "Boro season paddy."),
                StorageBooking(godowns[0], farmer, 45, -140, -50, BookingStatus.Completed, -145),
                StorageBooking(godowns[1], karim, 150, -100, -10, BookingStatus.Completed, -105, "Wheat storage before milling."),
                StorageBooking(godowns[2], salma, 30, -25, 5, BookingStatus.Rejected, -30, "Certified seed paddy for next season.", "Seed vault is fully booked until December."),
                StorageBooking(godowns[1], fatema, 120, -15, 75, BookingStatus.Accepted, -20, "Wheat storage before milling."),
                StorageBooking(godowns[0], motaleb, 60, -10, 50, BookingStatus.Accepted, -14),
                StorageBooking(godowns[0], farmer, 10, 3, 93, BookingStatus.Pending, 0, "10 tons of potato bags. Climate-controlled room needed for 3 months."),
                StorageBooking(godowns[1], salma, 40, 5, 95, BookingStatus.Pending, 0),
                StorageBooking(godowns[0], karim, 200, 10, 40, BookingStatus.Pending, -1, "Potato harvest, needs 2-8°C climate control."));
            await db.SaveChangesAsync();

            // Payouts settle specific completed bookings (oldest first); the newest completed booking is left unpaid
            // so the demo owners have a visible pending balance. Processing payouts are dated so the settlement
            // service completes them shortly after startup, and one failed transfer shows the retry path.
            var eqRepo = new EquipmentRevenueRepository(db);
            var eqCompleted = eqRepo.GetBookings(eqOwner.Id).Where(b => b.Status == BookingStatus.Completed).OrderBy(b => b.EndDate).ToList();
            Settle(eqRepo, eqOwner, "Equipment", eqCompleted.Take(2), "bKash", -95, PayoutStatus.Completed);
            Settle(eqRepo, eqOwner, "Equipment", eqCompleted.Skip(2).Take(1), "Nagad", -36, PayoutStatus.Failed, account: "01700000000");
            Settle(eqRepo, eqOwner, "Equipment", eqCompleted.Skip(2).Take(1), "Nagad", -35, PayoutStatus.Completed);
            Settle(eqRepo, eqOwner, "Equipment", eqCompleted.Skip(3).Take(1), "bKash", 0, PayoutStatus.Processing);

            var gdRepo = new GodownRevenueRepository(db);
            var gdCompleted = gdRepo.GetBookings(gdOwner.Id).Where(b => b.Status == BookingStatus.Completed).OrderBy(b => b.EndDate).ToList();
            Settle(gdRepo, gdOwner, "Godown", gdCompleted.Take(1), "Bank Transfer", -60, PayoutStatus.Completed);
            Settle(gdRepo, gdOwner, "Godown", gdCompleted.Skip(1).Take(1), "bKash", 0, PayoutStatus.Processing);

            var completedRental = await db.EquipmentBookings.FirstAsync(b => b.EquipmentId == equipment[1].Id && b.Status == BookingStatus.Completed);
            var completedStorage = await db.GodownBookings.FirstAsync(b => b.GodownId == godowns[1].Id && b.Status == BookingStatus.Completed);
            db.BookingExpenses.AddRange(
                new BookingExpense { BookingType = "Equipment", BookingId = completedRental.Id, OwnerId = eqOwner.Id, Amount = 2500, Note = "Diesel for harvester", RecordedOn = today.AddDays(-113) },
                new BookingExpense { BookingType = "Godown", BookingId = completedStorage.Id, OwnerId = gdOwner.Id, Amount = 4500, Note = "Fumigation before intake", RecordedOn = today.AddDays(-149) });
            await db.SaveChangesAsync();

            await SeedDemoPaymentsAndLedgerAsync(db);

            // Seed initial reviews for completed bookings
            await SeedDemoReviewsAsync(db);
        }

        private static async Task SeedDemoReviewsAsync(ApplicationDbContext db)
        {
            if (await db.Reviews.AnyAsync()) return;

            var eqBookings = await db.EquipmentBookings
                .Include(b => b.Equipment)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Completed)
                .ToListAsync();

            var gdBookings = await db.GodownBookings
                .Include(b => b.Godown)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Completed)
                .ToListAsync();

            var reviews = new List<Review>();

            // Harvester (Karim review)
            var karimHarvesterBooking = eqBookings.FirstOrDefault(b => b.Equipment!.Name.Contains("Harvester") && b.Farmer!.FullName == "Karim Mia");
            if (karimHarvesterBooking != null)
            {
                reviews.Add(new Review
                {
                    EquipmentBookingId = karimHarvesterBooking.Id,
                    EquipmentId = karimHarvesterBooking.EquipmentId,
                    FarmerId = karimHarvesterBooking.FarmerId,
                    Rating = 5,
                    Comment = "Harvester performed beyond expectations. Saved 3 days of manual labor for my paddy crop! Clean threshing and prompt owner.",
                    OwnerReply = "Thank you Karim bhai! We inspect and service all our combine harvesters before dispatch. Best wishes for your harvest.",
                    OwnerRepliedAt = karimHarvesterBooking.EndDate.AddDays(1).AddHours(4),
                    CreatedAt = karimHarvesterBooking.EndDate.AddDays(1)
                });
            }

            // Harvester (Rahim review)
            var rahimHarvesterBooking = eqBookings.FirstOrDefault(b => b.Equipment!.Name.Contains("Harvester") && b.Farmer!.FullName == "Rahim Uddin");
            if (rahimHarvesterBooking != null)
            {
                reviews.Add(new Review
                {
                    EquipmentBookingId = rahimHarvesterBooking.Id,
                    EquipmentId = rahimHarvesterBooking.EquipmentId,
                    FarmerId = rahimHarvesterBooking.FarmerId,
                    Rating = 5,
                    Comment = "Very smooth operation. Machine arrived with full tank and in pristine condition. Highly recommended for large fields.",
                    CreatedAt = rahimHarvesterBooking.EndDate.AddDays(1)
                });
            }

            // Tractor (Fatema review)
            var fatemaTractorBooking = eqBookings.FirstOrDefault(b => b.Equipment!.Name.Contains("Tractor") && b.Farmer!.FullName == "Fatema Begum");
            if (fatemaTractorBooking != null)
            {
                reviews.Add(new Review
                {
                    EquipmentBookingId = fatemaTractorBooking.Id,
                    EquipmentId = fatemaTractorBooking.EquipmentId,
                    FarmerId = fatemaTractorBooking.FarmerId,
                    Rating = 4,
                    Comment = "Good tractor with plenty of power for deep tilling. Fuel consumption was reasonable and attachment was sturdy.",
                    CreatedAt = fatemaTractorBooking.EndDate.AddDays(2)
                });
            }

            // Irrigation Pump (Motaleb review)
            var motalebPumpBooking = eqBookings.FirstOrDefault(b => b.Equipment!.Name.Contains("Pump") && b.Farmer!.FullName == "Motaleb Hossain");
            if (motalebPumpBooking != null)
            {
                reviews.Add(new Review
                {
                    EquipmentBookingId = motalebPumpBooking.Id,
                    EquipmentId = motalebPumpBooking.EquipmentId,
                    FarmerId = motalebPumpBooking.FarmerId,
                    Rating = 5,
                    Comment = "Strong flow rate and delivery hose was in great condition. Ran for continuous hours without overheating.",
                    CreatedAt = motalebPumpBooking.EndDate.AddDays(1)
                });
            }

            // Godown: AgriHub Warehouse (Motaleb review)
            var motalebWarehouseBooking = gdBookings.FirstOrDefault(b => b.Godown!.Name.Contains("AgriHub") && b.Farmer!.FullName == "Motaleb Hossain");
            if (motalebWarehouseBooking != null)
            {
                reviews.Add(new Review
                {
                    GodownBookingId = motalebWarehouseBooking.Id,
                    GodownId = motalebWarehouseBooking.GodownId,
                    FarmerId = motalebWarehouseBooking.FarmerId,
                    Rating = 5,
                    Comment = "Excellent warehouse facility. Platform is high enough for direct truck loading, and completely fumigated with zero pest damage.",
                    OwnerReply = "Thank you Motaleb bhai! Happy to provide secure storage for your produce anytime.",
                    OwnerRepliedAt = motalebWarehouseBooking.EndDate.AddDays(1).AddHours(6),
                    CreatedAt = motalebWarehouseBooking.EndDate.AddDays(1)
                });
            }

            // Godown: AgriHub Warehouse (Karim review)
            var karimWarehouseBooking = gdBookings.FirstOrDefault(b => b.Godown!.Name.Contains("AgriHub") && b.Farmer!.FullName == "Karim Mia");
            if (karimWarehouseBooking != null)
            {
                reviews.Add(new Review
                {
                    GodownBookingId = karimWarehouseBooking.Id,
                    GodownId = karimWarehouseBooking.GodownId,
                    FarmerId = karimWarehouseBooking.FarmerId,
                    Rating = 4,
                    Comment = "Safe storage for my wheat harvest. CCTV security, night guards, and good moisture control gave complete peace of mind.",
                    CreatedAt = karimWarehouseBooking.EndDate.AddDays(2)
                });
            }

            db.Reviews.AddRange(reviews);
            await db.SaveChangesAsync();

            // Recalculate and update AverageRating and ReviewCount on all Equipment and Godowns
            var equipmentList = await db.Equipment.ToListAsync();
            foreach (var eq in equipmentList)
            {
                var eqReviews = reviews.Where(r => r.EquipmentId == eq.Id).ToList();
                if (eqReviews.Any())
                {
                    eq.ReviewCount = eqReviews.Count;
                    eq.AverageRating = Math.Round((double)eqReviews.Average(r => r.Rating), 1);
                }
            }

            var godownList = await db.Godowns.ToListAsync();
            foreach (var gd in godownList)
            {
                var gdReviews = reviews.Where(r => r.GodownId == gd.Id).ToList();
                if (gdReviews.Any())
                {
                    gd.ReviewCount = gdReviews.Count;
                    gd.AverageRating = Math.Round((double)gdReviews.Average(r => r.Rating), 1);
                }
            }

            // Recalculate OwnerAverageRating and OwnerReviewCount across all owners
            var owners = await db.Users
                .Where(u => u.UserRole == AppRoles.EquipmentOwner || u.UserRole == AppRoles.GodownOwner)
                .ToListAsync();

            foreach (var owner in owners)
            {
                var ownerRatings = await db.Reviews
                    .Where(r => (r.Equipment != null && r.Equipment.OwnerId == owner.Id) || (r.Godown != null && r.Godown.OwnerId == owner.Id))
                    .Select(r => r.Rating)
                    .ToListAsync();

                owner.OwnerReviewCount = ownerRatings.Count;
                owner.OwnerAverageRating = ownerRatings.Count > 0 ? Math.Round(ownerRatings.Average(), 1) : 0.0;
            }

            await db.SaveChangesAsync();
        }

        private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string phone,
            string fullName, string role, string location, string specialization, string? business = null, bool isVerified = false, string? nid = null)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null) return existing;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                PhoneNumber = phone,
                FullName = fullName,
                UserRole = role,
                Location = location,
                BusinessOrFarmName = business,
                District = OnboardingOptions.GuessDistrict(location),
                Specialization = specialization,
                IsVerified = isVerified,
                VerificationStatus = isVerified ? "Verified" : "Unverified",
                NidNumber = nid,
                VerificationReviewedAt = isVerified ? DateTime.UtcNow.AddMonths(-6) : null,
                VerificationNotes = isVerified ? "Verified demo owner account." : null,
                OnboardingCompletedAt = DateTime.UtcNow.AddMonths(-8),
                CreatedAt = DateTime.UtcNow.AddMonths(-8)
            };

            var result = await userManager.CreateAsync(user, DemoCredential);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Seeding user '{email}' failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");

            await userManager.AddToRoleAsync(user, role);
            return user;
        }

        private static Equipment Machine(string name, string category, decimal daily, decimal hourly, string location, string description, string image, int createdDaysAgo, double? lat = null, double? lng = null, string? district = null)
        {
            var (defaultLat, defaultLng) = GeoLocationHelper.GetDistrictCoordinates(location);
            return new()
            {
                Name = name,
                Category = category,
                DailyRate = daily,
                HourlyRate = hourly,
                Location = location,
                District = district ?? OnboardingOptions.GuessDistrict(location),
                Latitude = lat ?? defaultLat,
                Longitude = lng ?? defaultLng,
                Description = description,
                ImageUrls = image,
                CreatedAt = DateTime.UtcNow.AddDays(createdDaysAgo)
            };
        }

        private static Godown Storage(string name, string type, double tons, decimal price, string location, string description, string facilities, string image, int createdDaysAgo, double? lat = null, double? lng = null, string? district = null)
        {
            var (defaultLat, defaultLng) = GeoLocationHelper.GetDistrictCoordinates(location);
            return new()
            {
                Name = name,
                StorageType = type,
                CapacityInTons = tons,
                PricePerTonPerMonth = price,
                Location = location,
                District = district ?? OnboardingOptions.GuessDistrict(location),
                Latitude = lat ?? defaultLat,
                Longitude = lng ?? defaultLng,
                Description = description,
                Facilities = facilities,
                ImageUrls = image,
                CreatedAt = DateTime.UtcNow.AddDays(createdDaysAgo)
            };
        }

        private static EquipmentBooking Rental(Equipment e, ApplicationUser farmer, int startOffset, int endOffset, string status, int requestedOffset, string? note = null, string? reject = null)
        {
            var b = new EquipmentBooking
            {
                EquipmentId = e.Id,
                FarmerId = farmer.Id,
                Status = status,
                Note = note,
                RejectReason = reject,
                StartDate = DateTime.Today.AddDays(startOffset),
                EndDate = DateTime.Today.AddDays(endOffset),
                RequestedOn = DateTime.Now.AddDays(requestedOffset).AddHours(-2),
                UpdatedOn = status == BookingStatus.Pending ? null : DateTime.Now.AddDays(requestedOffset).AddHours(4)
            };
            Snapshot(b, e.DailyRate, BookingPricing.EquipmentGross(b.StartDate, b.EndDate, e.DailyRate));
            return b;
        }

        private static GodownBooking StorageBooking(Godown g, ApplicationUser farmer, double tons, int startOffset, int endOffset, string status, int requestedOffset, string? note = null, string? reject = null)
        {
            var b = new GodownBooking
            {
                GodownId = g.Id,
                FarmerId = farmer.Id,
                StorageTons = tons,
                Status = status,
                Note = note,
                RejectReason = reject,
                StartDate = DateTime.Today.AddDays(startOffset),
                EndDate = DateTime.Today.AddDays(endOffset),
                RequestedOn = DateTime.Now.AddDays(requestedOffset).AddHours(-3),
                UpdatedOn = status == BookingStatus.Pending ? null : DateTime.Now.AddDays(requestedOffset).AddHours(5)
            };
            Snapshot(b, g.PricePerTonPerMonth, BookingPricing.GodownGross(b.StartDate, b.EndDate, tons, g.PricePerTonPerMonth));
            return b;
        }

        /// <summary>Mirrors what RespondAsync records at acceptance, so seeded history obeys the same rules as live data.</summary>
        private static void Snapshot(IPayableBooking b, decimal rate, decimal gross)
        {
            if (b.Status is BookingStatus.Pending or BookingStatus.Rejected) return;
            b.AgreedRate = rate;
            b.AgreedGross = gross;
            b.CommissionRate = SeedCommissionRate;
            if (b.Status == BookingStatus.Completed) b.CompletedOn = b.EndDate.AddHours(17);
        }

        /// <summary>Creates one payout covering the given bookings (commission from each booking's snapshot) and links them to it.</summary>
        private static void Settle(IOwnerRevenueRepository repo, ApplicationUser owner, string listingType, IEnumerable<RevenueBooking> bookings, string method, int daysAgo, string status, string? account = null)
        {
            var list = bookings.ToList();
            if (list.Count == 0) return;

            var gross = list.Sum(b => b.Gross);
            var commission = list.Sum(b => BookingPricing.Commission(b.Gross, b.CommissionRateSnapshot));
            var date = daysAgo == 0 ? DateTime.Now.AddSeconds(-90) : DateTime.Today.AddDays(daysAgo);
            var payoutId = repo.AddPayout(new Transaction
            {
                UserId = owner.Id,
                Reference = $"KL-PO-{date:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                ListingType = listingType,
                GrossAmount = gross,
                Commission = commission,
                Amount = gross - commission,
                PaymentMethod = method,
                PayoutAccount = account ?? (method == "Bank Transfer" ? "0123456789012" : owner.PhoneNumber),
                Status = status,
                TransactionDate = date,
                SettledOn = status == PayoutStatus.Processing ? null : date.AddHours(6),
                FailureReason = status == PayoutStatus.Failed ? BLL.Services.PayoutSettlementService.RejectedAccountReason : null
            });
            // A failed transfer leaves its bookings in the owed balance, exactly as the settlement service does.
            if (status != PayoutStatus.Failed) repo.MarkBookingsPaid(list.Select(b => b.Id), payoutId);
        }

        private const decimal SeedCommissionRate = 0.05m;

        /// <summary>
        /// Gives every seeded Completed booking (and one Accepted booking per type) a succeeded escrow payment, adds one failed
        /// attempt for history, then writes the ledger rows that the live flow would have produced for each event.
        /// </summary>
        private static async Task SeedDemoPaymentsAndLedgerAsync(ApplicationDbContext db)
        {
            var rentals = await db.EquipmentBookings.Include(b => b.Equipment).Where(b => b.Status != BookingStatus.Pending).ToListAsync();
            var storage = await db.GodownBookings.Include(b => b.Godown).Where(b => b.Status != BookingStatus.Pending).ToListAsync();

            var methods = PaymentMethods.All;
            var i = 0;
            Payment Pay(IPayableBooking b, string type, string status, DateTime on, string? failure = null) => new()
            {
                BookingType = type,
                BookingId = b.Id,
                FarmerId = b.FarmerId,
                Amount = b.AgreedGross ?? 0,
                Method = methods[i++ % methods.Length],
                PayerAccount = "017" + (10000000 + b.Id * 7919).ToString()[..8],
                Reference = $"KL-PM-{on:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                GatewayReference = "SIM-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                Status = status,
                CreatedOn = on.AddMinutes(-3),
                PaidOn = status == PaymentStatus.Succeeded ? on : null,
                FailureReason = failure
            };

            var payments = new List<(Payment Payment, IPayableBooking Booking)>();
            foreach (var b in rentals.Where(b => b.Status == BookingStatus.Completed))
                payments.Add((Pay(b, "Equipment", PaymentStatus.Succeeded, b.StartDate.AddDays(-1).AddHours(10)), b));
            foreach (var b in storage.Where(b => b.Status == BookingStatus.Completed))
                payments.Add((Pay(b, "Godown", PaymentStatus.Succeeded, b.StartDate.AddDays(-1).AddHours(11)), b));

            // One paid + one unpaid Accepted booking per type so the demo shows both "Payment required" and "Paid".
            var paidRental = rentals.Where(b => b.Status == BookingStatus.Accepted).OrderBy(b => b.StartDate).First();
            var paidStorage = storage.Where(b => b.Status == BookingStatus.Accepted).OrderBy(b => b.StartDate).First();
            payments.Add((Pay(paidRental, "Equipment", PaymentStatus.Succeeded, paidRental.UpdatedOn!.Value.AddHours(3)), paidRental));
            payments.Add((Pay(paidStorage, "Godown", PaymentStatus.Succeeded, paidStorage.UpdatedOn!.Value.AddHours(2)), paidStorage));

            var failedOn = rentals.Where(b => b.Status == BookingStatus.Accepted && b.Id != paidRental.Id).First();
            db.Payments.Add(Pay(failedOn, "Equipment", PaymentStatus.Failed, failedOn.UpdatedOn!.Value.AddHours(1), "Insufficient balance (simulated)"));

            foreach (var (p, b) in payments)
            {
                db.Payments.Add(p);
                b.Payment = p;
                b.PaidOn = p.PaidOn;
                if (b.Status == BookingStatus.Accepted) b.Status = BookingStatus.Paid;
            }
            await db.SaveChangesAsync();

            // Ledger: PaymentIn per succeeded payment, CommissionEarned per completed booking, PayoutOut per completed payout.
            foreach (var (p, _) in payments) db.LedgerEntries.Add(LedgerPostings.PaymentIn(p));
            foreach (var b in rentals.Where(b => b.Status == BookingStatus.Completed)) db.LedgerEntries.Add(LedgerPostings.CommissionEarned("Equipment", b, b.Equipment!.OwnerId));
            foreach (var b in storage.Where(b => b.Status == BookingStatus.Completed)) db.LedgerEntries.Add(LedgerPostings.CommissionEarned("Godown", b, b.Godown!.OwnerId));
            foreach (var t in await db.Transactions.Where(t => t.Status == PayoutStatus.Completed).ToListAsync()) db.LedgerEntries.Add(LedgerPostings.PayoutOut(t));
            await db.SaveChangesAsync();
        }

        /// <summary>
        /// Idempotent: bookings accepted before price snapshots existed get one computed from the current listing price,
        /// so revenue, invoices and payouts stop depending on live rates. Returns how many rows were updated.
        /// </summary>
        private static async Task<int> BackfillPriceSnapshotsAsync(ApplicationDbContext db, decimal commissionRate)
        {
            var priced = new[] { BookingStatus.Accepted, BookingStatus.Paid, BookingStatus.Completed };
            var rentals = await db.EquipmentBookings.Include(b => b.Equipment)
                .Where(b => b.AgreedGross == null && priced.Contains(b.Status))
                .ToListAsync();
            foreach (var b in rentals)
            {
                b.AgreedRate = b.Equipment!.DailyRate;
                b.AgreedGross = BookingPricing.EquipmentGross(b.StartDate, b.EndDate, b.Equipment.DailyRate);
                b.CommissionRate = commissionRate;
            }

            var storage = await db.GodownBookings.Include(b => b.Godown)
                .Where(b => b.AgreedGross == null && priced.Contains(b.Status))
                .ToListAsync();
            foreach (var b in storage)
            {
                b.AgreedRate = b.Godown!.PricePerTonPerMonth;
                b.AgreedGross = BookingPricing.GodownGross(b.StartDate, b.EndDate, b.StorageTons, b.Godown.PricePerTonPerMonth);
                b.CommissionRate = commissionRate;
            }

            var count = rentals.Count + storage.Count;
            if (count > 0) await db.SaveChangesAsync();
            return count;
        }

        private static async Task SeedDemoLoyaltyPointsAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            var farmer = await userManager.FindByEmailAsync("farmer@krishilink.com");
            if (farmer != null && (!await db.LoyaltyPointTransactions.AnyAsync(t => t.UserId == farmer.Id) || farmer.LoyaltyPoints == 0))
            {
                var eqBooking = await db.EquipmentBookings.FirstOrDefaultAsync(b => b.FarmerId == farmer.Id && b.Status == BookingStatus.Completed);
                var gdBooking = await db.GodownBookings.FirstOrDefaultAsync(b => b.FarmerId == farmer.Id && b.Status == BookingStatus.Completed);

                var transactions = new List<LoyaltyPointTransaction>
                {
                    new()
                    {
                        UserId = farmer.Id,
                        Points = 50,
                        Type = LoyaltyTransactionTypes.Bonus,
                        Description = "Welcome bonus points for joining KrishiLink",
                        CreatedAt = DateTime.UtcNow.AddMonths(-2)
                    }
                };

                if (eqBooking != null)
                {
                    transactions.Add(new()
                    {
                        UserId = farmer.Id,
                        Points = 125,
                        Type = LoyaltyTransactionTypes.Earned,
                        Description = $"Earned 125 points for completed Equipment Rental (#EQ-{eqBooking.Id:D4})",
                        BookingType = "Equipment",
                        BookingId = eqBooking.Id,
                        BookingCode = $"#EQ-{eqBooking.Id:D4}",
                        AmountSpent = 12500m,
                        CreatedAt = DateTime.UtcNow.AddDays(-28)
                    });
                }

                if (gdBooking != null)
                {
                    transactions.Add(new()
                    {
                        UserId = farmer.Id,
                        Points = 75,
                        Type = LoyaltyTransactionTypes.Earned,
                        Description = $"Earned 75 points for completed Godown Storage (#GD-{gdBooking.Id:D4})",
                        BookingType = "Godown",
                        BookingId = gdBooking.Id,
                        BookingCode = $"#GD-{gdBooking.Id:D4}",
                        AmountSpent = 7500m,
                        CreatedAt = DateTime.UtcNow.AddDays(-40)
                    });
                }

                farmer.LoyaltyPoints = transactions.Sum(t => t.Points);
                db.LoyaltyPointTransactions.AddRange(transactions);
                await db.SaveChangesAsync();
                await userManager.UpdateAsync(farmer);
            }

            var karim = await userManager.FindByEmailAsync("karim.mia@krishilink.com");
            if (karim != null && (!await db.LoyaltyPointTransactions.AnyAsync(t => t.UserId == karim.Id) || karim.LoyaltyPoints == 0))
            {
                var transactions = new List<LoyaltyPointTransaction>
                {
                    new()
                    {
                        UserId = karim.Id,
                        Points = 50,
                        Type = LoyaltyTransactionTypes.Bonus,
                        Description = "Welcome bonus points for joining KrishiLink",
                        CreatedAt = DateTime.UtcNow.AddMonths(-2)
                    },
                    new()
                    {
                        UserId = karim.Id,
                        Points = 85,
                        Type = LoyaltyTransactionTypes.Earned,
                        Description = "Earned 85 points for completed Equipment Rental",
                        AmountSpent = 8500m,
                        CreatedAt = DateTime.UtcNow.AddDays(-20)
                    }
                };
                karim.LoyaltyPoints = transactions.Sum(t => t.Points);
                db.LoyaltyPointTransactions.AddRange(transactions);
                await db.SaveChangesAsync();
                await userManager.UpdateAsync(karim);
            }
        }

        private static async Task SeedCropCalendarAsync(ApplicationDbContext db)
        {
            if (await db.CropCalendarEntries.AnyAsync()) return;

            var entries = new List<CropCalendarEntry>
            {
                new()
                {
                    Name = "Boro Rice (HYV & Hybrid)",
                    BanglaName = "বোরো ধান (উফশী ও হাইব্রিড)",
                    ScientificName = "Oryza sativa",
                    Category = "Cereals",
                    Season = "Rabi",
                    ProfileCropName = "Rice (Boro)",
                    SowingMonths = new List<int> { 11, 12, 1 },
                    GrowingMonths = new List<int> { 1, 2, 3 },
                    HarvestingMonths = new List<int> { 4, 5 },
                    DurationDays = "140 – 160 Days",
                    OptimalTemperature = "20°C – 32°C",
                    SoilTypes = "Clay Loam, Alluvial Silt (এঁটেল-দোআঁশ)",
                    WaterRequirement = "High — continuous standing water (3-5 cm) during tillering",
                    PopularVarieties = "BRRI dhan 28, BRRI dhan 29, BRRI dhan 89, BRRI dhan 92, Bangabandhu dhan 100",
                    MajorDistricts = "Mymensingh, Bogra, Dinajpur, Naogaon, Kishoreganj, Sunamganj (Haor)",
                    Division = "All",
                    KeyTips = "Transplant 30-35 day seedlings with 2-3 seedlings per hill. Apply urea in 3 equal splits. Drain water 10-12 days before harvest.",
                    IconClass = "bi-flower2",
                    BadgeColor = "success"
                },
                new()
                {
                    Name = "T. Aman Rice (Transplanted Aman)",
                    BanglaName = "রোপা আমন ধান (উফশী)",
                    ScientificName = "Oryza sativa",
                    Category = "Cereals",
                    Season = "Kharif-2",
                    ProfileCropName = "Rice (Aman)",
                    SowingMonths = new List<int> { 6, 7, 8 },
                    GrowingMonths = new List<int> { 8, 9, 10 },
                    HarvestingMonths = new List<int> { 11, 12 },
                    DurationDays = "115 – 140 Days",
                    OptimalTemperature = "22°C – 35°C",
                    SoilTypes = "Clay Loam, Silt Loam (দোআঁশ ও এঁটেল)",
                    WaterRequirement = "Medium to High — mainly rainfed with supplemental irrigation during dry spells",
                    PopularVarieties = "BRRI dhan 49, BRRI dhan 75, BRRI dhan 87, Bina dhan-7, Swarna",
                    MajorDistricts = "Rangpur, Dinajpur, Bogra, Rajshahi, Jessore, Barisal, Comilla",
                    Division = "All",
                    KeyTips = "Transplant 25-30 day seedlings by August 15 for optimal yield. Watch for stem borer and rice blast during humid vegetative stage.",
                    IconClass = "bi-flower2",
                    BadgeColor = "success"
                },
                new()
                {
                    Name = "Aus Rice (Upland & Transplanted)",
                    BanglaName = "আউশ ধান (বোনা ও রোপা)",
                    ScientificName = "Oryza sativa",
                    Category = "Cereals",
                    Season = "Kharif-1",
                    ProfileCropName = "Rice (Aus)",
                    SowingMonths = new List<int> { 3, 4 },
                    GrowingMonths = new List<int> { 4, 5, 6 },
                    HarvestingMonths = new List<int> { 6, 7 },
                    DurationDays = "95 – 110 Days",
                    OptimalTemperature = "25°C – 35°C",
                    SoilTypes = "Sandy Loam, Alluvial Silt (বেলে-দোআঁশ)",
                    WaterRequirement = "Medium — relies on pre-monsoon rains (Kalbaishakhi)",
                    PopularVarieties = "BRRI dhan 48, BRRI dhan 82, BRRI dhan 85, Nerica-1",
                    MajorDistricts = "Kushtia, Meherpur, Comilla, Chittagong, Sylhet, Mymensingh",
                    Division = "All",
                    KeyTips = "Fast maturing crop fits neatly between Boro and Aman. Early weeding is crucial in direct seeded Aus plots.",
                    IconClass = "bi-flower2",
                    BadgeColor = "success"
                },
                new()
                {
                    Name = "High-Yield Wheat",
                    BanglaName = "উচ্চফলনশীল গম",
                    ScientificName = "Triticum aestivum",
                    Category = "Cereals",
                    Season = "Rabi",
                    ProfileCropName = "Wheat",
                    SowingMonths = new List<int> { 11, 12 },
                    GrowingMonths = new List<int> { 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4 },
                    DurationDays = "105 – 115 Days",
                    OptimalTemperature = "15°C – 25°C",
                    SoilTypes = "Well-drained Loam, Sandy Loam (সুনিষ্কাশিত দোআঁশ)",
                    WaterRequirement = "Low to Medium — 3 light irrigations (CRI stage at 18-21 days, flowering, and grain fill)",
                    PopularVarieties = "BARI Gom 30, BARI Gom 32, BARI Gom 33 (Blast Resistant), WMRI Gom 1",
                    MajorDistricts = "Dinajpur, Thakurgaon, Rajshahi, Pabna, Kushtia, Chuadanga, Meherpur",
                    Division = "Rajshahi, Rangpur, Khulna",
                    KeyTips = "Sow before December 10 to avoid terminal heat stress in March. Use certified blast-resistant BARI Gom 33 seed.",
                    IconClass = "bi-tsunami",
                    BadgeColor = "warning"
                },
                new()
                {
                    Name = "Hybrid Maize (Corn)",
                    BanglaName = "হাইব্রিড ভুট্টা",
                    ScientificName = "Zea mays",
                    Category = "Cereals",
                    Season = "Rabi",
                    ProfileCropName = "Maize",
                    SowingMonths = new List<int> { 10, 11, 12 },
                    GrowingMonths = new List<int> { 11, 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4, 5 },
                    DurationDays = "135 – 145 Days",
                    OptimalTemperature = "18°C – 32°C",
                    SoilTypes = "Deep fertile Loam, Clay Loam (উর্বর দোআঁশ)",
                    WaterRequirement = "Medium — furrow irrigation at knee-high, tasseling, and grain filling stages",
                    PopularVarieties = "BARI Hybrid Maize 9, BARI Hybrid Maize 11, PAC 759, Pioneer 3355, Sunshine-55",
                    MajorDistricts = "Dinajpur, Chuadanga, Lalmonirhat, Bogra, Manikganj, Rajshahi",
                    Division = "All",
                    KeyTips = "Maintain 60 cm row and 20 cm plant spacing. Monitor weekly for Fall Armyworm and install pheromone traps.",
                    IconClass = "bi-sun-fill",
                    BadgeColor = "warning"
                },
                new()
                {
                    Name = "Potato (Winter Table & Seed)",
                    BanglaName = "গোল আলু (ডায়মন্ট ও কার্ডিনাল)",
                    ScientificName = "Solanum tuberosum",
                    Category = "Tubers",
                    Season = "Rabi",
                    ProfileCropName = "Potato",
                    SowingMonths = new List<int> { 10, 11, 12 },
                    GrowingMonths = new List<int> { 11, 12, 1 },
                    HarvestingMonths = new List<int> { 1, 2, 3 },
                    DurationDays = "85 – 100 Days",
                    OptimalTemperature = "15°C – 22°C (Cool nights needed for tuberization)",
                    SoilTypes = "Sandy Loam, Silt Loam with organic matter (বেলে-দোআঁশ)",
                    WaterRequirement = "Medium — 3-4 light irrigations; strictly avoid waterlogging",
                    PopularVarieties = "Diamant, Cardinal, Granola, Asterix, BARI Alu 7, BARI Alu 41",
                    MajorDistricts = "Munshiganj, Bogra, Rangpur, Dinajpur, Joypurhat, Nilphamari",
                    Division = "All",
                    KeyTips = "Earthing-up at 25 and 45 days. Stop irrigation 10-12 days before harvest and dehaulm to harden tuber skin.",
                    IconClass = "bi-circle-fill",
                    BadgeColor = "primary"
                },
                new()
                {
                    Name = "Jute (Toshe & Deshi Golden Fiber)",
                    BanglaName = "তোষা ও দেশী পাট (সোনালী আঁশ)",
                    ScientificName = "Corchorus olitorius / capsularis",
                    Category = "CashCrops",
                    Season = "Kharif-1",
                    ProfileCropName = "Jute",
                    SowingMonths = new List<int> { 3, 4, 5 },
                    GrowingMonths = new List<int> { 4, 5, 6, 7 },
                    HarvestingMonths = new List<int> { 7, 8, 9 },
                    DurationDays = "110 – 120 Days",
                    OptimalTemperature = "24°C – 37°C with high humidity",
                    SoilTypes = "Alluvial Silt, Clay Loam (পলি ও এঁটেল দোআঁশ)",
                    WaterRequirement = "High — thrives in heavy monsoon rains and needs slow-moving retting water",
                    PopularVarieties = "O-9897 (Toshe), Robi-1, BJRI Deshi Pat 8, Chaitali Pat",
                    MajorDistricts = "Faridpur, Jessore, Rajbari, Jamalpur, Sirajganj, Mymensingh, Rangpur",
                    Division = "All",
                    KeyTips = "Harvest when 50% plants are in pod formation for strongest fiber. Ret in clean, slow-moving water for bright golden sheen.",
                    IconClass = "bi-layers-fill",
                    BadgeColor = "success"
                },
                new()
                {
                    Name = "Mustard & Rapeseed",
                    BanglaName = "উচ্চফলনশীল সরিষা",
                    ScientificName = "Brassica napus / campestris",
                    Category = "Oilseeds",
                    Season = "Rabi",
                    ProfileCropName = "Oilseeds",
                    SowingMonths = new List<int> { 10, 11 },
                    GrowingMonths = new List<int> { 11, 12, 1 },
                    HarvestingMonths = new List<int> { 1, 2 },
                    DurationDays = "70 – 85 Days",
                    OptimalTemperature = "15°C – 25°C",
                    SoilTypes = "Loam, Sandy Loam (দোআঁশ ও বেলে-দোআঁশ)",
                    WaterRequirement = "Low — 1-2 light irrigations at pre-flowering and pod filling",
                    PopularVarieties = "BARI Sharisha 14, BARI Sharisha 17, BARI Sharisha 18, Bina Sharisha 4",
                    MajorDistricts = "Tangail, Sirajganj, Manikganj, Jessore, Magura, Comilla",
                    Division = "All",
                    KeyTips = "Short duration crop perfectly bridges Aman harvest and late Boro transplanting. Control aphids during yellow bloom.",
                    IconClass = "bi-brightness-high-fill",
                    BadgeColor = "warning"
                },
                new()
                {
                    Name = "Lentil (Masur Dal)",
                    BanglaName = "মসুর ডাল",
                    ScientificName = "Lens culinaris",
                    Category = "Pulses",
                    Season = "Rabi",
                    ProfileCropName = "Pulses",
                    SowingMonths = new List<int> { 10, 11 },
                    GrowingMonths = new List<int> { 11, 12, 1 },
                    HarvestingMonths = new List<int> { 2, 3 },
                    DurationDays = "100 – 110 Days",
                    OptimalTemperature = "15°C – 25°C",
                    SoilTypes = "Well-drained Loam, Silt Loam (সুনিষ্কাশিত দোআঁশ)",
                    WaterRequirement = "Low — mostly rainfed residual moisture; sensitive to water stagnation",
                    PopularVarieties = "BARI Masur 6, BARI Masur 7, BARI Masur 8, Bina Masur 5",
                    MajorDistricts = "Faridpur, Jessore, Kushtia, Rajshahi, Magura, Pabna",
                    Division = "Rajshahi, Khulna, Dhaka",
                    KeyTips = "Treat seeds with bio-fertilizer (Rhizobium) before sowing. Harvest in morning when pods are brown to prevent shattering.",
                    IconClass = "bi-dot",
                    BadgeColor = "primary"
                },
                new()
                {
                    Name = "Chickpea (Chhola)",
                    BanglaName = "ছোলা",
                    ScientificName = "Cicer arietinum",
                    Category = "Pulses",
                    Season = "Rabi",
                    ProfileCropName = "Pulses",
                    SowingMonths = new List<int> { 10, 11 },
                    GrowingMonths = new List<int> { 11, 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4 },
                    DurationDays = "120 – 130 Days",
                    OptimalTemperature = "18°C – 26°C",
                    SoilTypes = "Deep Loam, Clay Loam in Barind areas (বরেন্দ্র অঞ্চলের দোআঁশ)",
                    WaterRequirement = "Low — drought hardy; requires zero standing water",
                    PopularVarieties = "BARI Chhola 5, BARI Chhola 9, BARI Chhola 10",
                    MajorDistricts = "Rajshahi, Chapainawabganj, Naogaon, Kushtia, Jessore",
                    Division = "Rajshahi, Khulna",
                    KeyTips = "Excellent cash pulse for dry high Barind tract after early Aman rice harvest.",
                    IconClass = "bi-dot",
                    BadgeColor = "primary"
                },
                new()
                {
                    Name = "Mungbean (Mug Dal)",
                    BanglaName = "মুগ ডাল (গ্রীষ্ম ও খরিফ)",
                    ScientificName = "Vigna radiata",
                    Category = "Pulses",
                    Season = "Kharif-1",
                    ProfileCropName = "Pulses",
                    SowingMonths = new List<int> { 2, 3, 8 },
                    GrowingMonths = new List<int> { 3, 4, 9 },
                    HarvestingMonths = new List<int> { 4, 5, 10 },
                    DurationDays = "60 – 65 Days",
                    OptimalTemperature = "25°C – 35°C",
                    SoilTypes = "Well-drained Sandy Loam, Silt Loam (বেলে-দোআঁশ)",
                    WaterRequirement = "Low to Medium — short duration pulse",
                    PopularVarieties = "BARI Mung 6, BARI Mung 8, Bina Mung 8",
                    MajorDistricts = "Patuakhali, Bhola, Barisal, Jhenaidah, Jessore, Natore",
                    Division = "Barisal, Khulna, Rajshahi",
                    KeyTips = "Short 60-day crop. Pods can be picked in 2 flushes. Crop residue enriches soil nitrogen.",
                    IconClass = "bi-dot",
                    BadgeColor = "primary"
                },
                new()
                {
                    Name = "Winter Onion",
                    BanglaName = "শীতকালীন পেঁয়াজ (তাহেরপুরী ও বারি)",
                    ScientificName = "Allium cepa",
                    Category = "Spices",
                    Season = "Rabi",
                    ProfileCropName = "Spices",
                    SowingMonths = new List<int> { 10, 11, 12 },
                    GrowingMonths = new List<int> { 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4 },
                    DurationDays = "90 – 105 Days after transplanting",
                    OptimalTemperature = "13°C – 24°C",
                    SoilTypes = "Fertile Sandy Loam with high organic matter (উর্বর বেলে-দোআঁশ)",
                    WaterRequirement = "Medium — regular light irrigations; stop 15 days before harvest",
                    PopularVarieties = "BARI Piaz 1, BARI Piaz 4, Taherpuri, Faridpuri Bhati",
                    MajorDistricts = "Pabna, Faridpur, Rajshahi, Kushtia, Natore, Manikganj",
                    Division = "All",
                    KeyTips = "Cure bulbs in shade for 3-5 days after digging. Ensure godown storage is well-ventilated and dry.",
                    IconClass = "bi-record-circle-fill",
                    BadgeColor = "danger"
                },
                new()
                {
                    Name = "Summer & Monsoon Onion",
                    BanglaName = "গ্রীষ্মকালীন ও বর্ষাকালীন পেঁয়াজ",
                    ScientificName = "Allium cepa",
                    Category = "Spices",
                    Season = "Kharif-1",
                    ProfileCropName = "Spices",
                    SowingMonths = new List<int> { 2, 3, 4 },
                    GrowingMonths = new List<int> { 3, 4, 5 },
                    HarvestingMonths = new List<int> { 6, 7, 8 },
                    DurationDays = "90 – 100 Days",
                    OptimalTemperature = "25°C – 35°C",
                    SoilTypes = "Raised Bed Sandy Loam (উঁচু বেড বেলে-দোআঁশ)",
                    WaterRequirement = "Medium — requires polythene rain shelters or raised bed drainage during monsoon showers",
                    PopularVarieties = "BARI Piaz 5, Summer King",
                    MajorDistricts = "Kushtia, Meherpur, Pabna, Rajshahi, Bogra",
                    Division = "Khulna, Rajshahi",
                    KeyTips = "High-profit off-season crop. Raised bed cultivation with transparent polythene tunnel prevents bulb rotting.",
                    IconClass = "bi-record-circle-fill",
                    BadgeColor = "danger"
                },
                new()
                {
                    Name = "Garlic (Winter & Zero Tillage)",
                    BanglaName = "রসুন (বিনা চাষ ও সাধারণ)",
                    ScientificName = "Allium sativum",
                    Category = "Spices",
                    Season = "Rabi",
                    ProfileCropName = "Spices",
                    SowingMonths = new List<int> { 10, 11 },
                    GrowingMonths = new List<int> { 11, 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4 },
                    DurationDays = "120 – 135 Days",
                    OptimalTemperature = "15°C – 25°C",
                    SoilTypes = "Clay Loam, Silt Loam with paddy straw mulch (কাদা দোআঁশ)",
                    WaterRequirement = "Medium — zero tillage method preserves mud moisture under straw",
                    PopularVarieties = "BARI Roshun 1, BARI Roshun 2, Natore Local, Chalanbeel Local",
                    MajorDistricts = "Natore (Gurudaspur, Baraigram), Pabna, Rajshahi, Dinajpur",
                    Division = "Rajshahi, Rangpur",
                    KeyTips = "Zero-tillage garlic on muddy soil directly after Aman harvest covered by rice straw mulch cuts cost by 40%.",
                    IconClass = "bi-record-circle-fill",
                    BadgeColor = "danger"
                },
                new()
                {
                    Name = "Chili / Green & Red Pepper",
                    BanglaName = "কাঁচা ও শুকনো মরিচ",
                    ScientificName = "Capsicum annuum",
                    Category = "Spices",
                    Season = "Rabi",
                    ProfileCropName = "Spices",
                    SowingMonths = new List<int> { 10, 11, 3 },
                    GrowingMonths = new List<int> { 11, 12, 4 },
                    HarvestingMonths = new List<int> { 1, 2, 3, 4, 5 },
                    DurationDays = "150 – 180 Days",
                    OptimalTemperature = "20°C – 30°C",
                    SoilTypes = "Sandy Loam, Alluvial Riverbed (বেলে-দোআঁশ ও চর অঞ্চল)",
                    WaterRequirement = "Medium — sensitive to excess water and root rot",
                    PopularVarieties = "BARI Morich 1, BARI Morich 2, Bogra Bindu, Jamalpur Local",
                    MajorDistricts = "Bogra (Sariakandi), Jamalpur, Chandpur, Faridpur, Panchagarh",
                    Division = "All",
                    KeyTips = "Multiple pickings throughout spring. Dry on clean concrete yards or solar dryers for premium red color.",
                    IconClass = "bi-fire",
                    BadgeColor = "danger"
                },
                new()
                {
                    Name = "Winter Tomato (HYV & Hybrid)",
                    BanglaName = "শীতকালীন টমেটো (উফশী ও হাইব্রিড)",
                    ScientificName = "Solanum lycopersicum",
                    Category = "Vegetables",
                    Season = "Rabi",
                    ProfileCropName = "Vegetables",
                    SowingMonths = new List<int> { 9, 10, 11 },
                    GrowingMonths = new List<int> { 11, 12 },
                    HarvestingMonths = new List<int> { 1, 2, 3 },
                    DurationDays = "90 – 110 Days",
                    OptimalTemperature = "18°C – 27°C (Nights of 15–18°C trigger flowering)",
                    SoilTypes = "Rich Loam, Sandy Loam (উর্বর দোআঁশ)",
                    WaterRequirement = "Medium — regular furrow irrigation; avoid splashing leaves to prevent blight",
                    PopularVarieties = "BARI Tomato 14, BARI Tomato 15, Ratan, Bahar, Beautiful",
                    MajorDistricts = "Jessore, Comilla, Dinajpur, Bogra, Rajshahi, Chittagong",
                    Division = "All",
                    KeyTips = "Staking with bamboo poles improves fruit size and prevents fungal soil rot. Apply Boron to stop fruit cracking.",
                    IconClass = "bi-egg-fill",
                    BadgeColor = "info"
                },
                new()
                {
                    Name = "Brinjal / Eggplant (Aubergine)",
                    BanglaName = "বেগুন (উফশী ও বিটি বেগুন)",
                    ScientificName = "Solanum melongena",
                    Category = "Vegetables",
                    Season = "YearRound",
                    ProfileCropName = "Vegetables",
                    SowingMonths = new List<int> { 8, 9, 10, 4 },
                    GrowingMonths = new List<int> { 10, 11, 5 },
                    HarvestingMonths = new List<int> { 11, 12, 1, 2, 6, 7 },
                    DurationDays = "130 – 160 Days",
                    OptimalTemperature = "22°C – 32°C",
                    SoilTypes = "Deep fertile Silt Loam, Clay Loam (গভীর উর্বর দোআঁশ)",
                    WaterRequirement = "Medium — regular irrigation at 10-12 day intervals",
                    PopularVarieties = "Bt Brinjal 1-4, BARI Begun 8, BARI Begun 10, Singnath, Islampuri",
                    MajorDistricts = "Jessore, Bogra, Mymensingh, Rangpur, Comilla, Jamalpur",
                    Division = "All",
                    KeyTips = "Bt Brinjal offers 100% natural immunity against Fruit and Shoot Borer without toxic chemical sprays.",
                    IconClass = "bi-egg-fill",
                    BadgeColor = "info"
                },
                new()
                {
                    Name = "Cabbage & Cauliflower",
                    BanglaName = "বাঁধাকপি ও ফুলকপি",
                    ScientificName = "Brassica oleracea",
                    Category = "Vegetables",
                    Season = "Rabi",
                    ProfileCropName = "Vegetables",
                    SowingMonths = new List<int> { 9, 10, 11 },
                    GrowingMonths = new List<int> { 11, 12 },
                    HarvestingMonths = new List<int> { 12, 1, 2 },
                    DurationDays = "75 – 90 Days",
                    OptimalTemperature = "15°C – 22°C",
                    SoilTypes = "Heavy Loam, Clay Loam with rich compost (সারসমৃদ্ধ এঁটেল-দোআঁশ)",
                    WaterRequirement = "Medium — continuous soil moisture needed for compact head formation",
                    PopularVarieties = "Snow White, Green Express, Atlas, BARI Fulkopi 1, BARI Bandhakopi 2",
                    MajorDistricts = "Bogra, Jessore, Rangpur, Comilla, Dhaka, Rajshahi",
                    Division = "All",
                    KeyTips = "Tie outer leaves around cauliflower curds (blanching) 5-7 days before harvest for spotless white heads.",
                    IconClass = "bi-circle-square",
                    BadgeColor = "info"
                },
                new()
                {
                    Name = "Country Bean (Sheem)",
                    BanglaName = "শিম (দেশী ও উফশী)",
                    ScientificName = "Lablab purpureus",
                    Category = "Vegetables",
                    Season = "Rabi",
                    ProfileCropName = "Vegetables",
                    SowingMonths = new List<int> { 6, 7, 8 },
                    GrowingMonths = new List<int> { 9, 10, 11 },
                    HarvestingMonths = new List<int> { 11, 12, 1, 2, 3 },
                    DurationDays = "140 – 180 Days",
                    OptimalTemperature = "18°C – 28°C",
                    SoilTypes = "Fertile Sandy Loam, Clay Loam (উর্বর দোআঁশ)",
                    WaterRequirement = "Medium — trellis cultivation with trench drainage",
                    PopularVarieties = "BARI Sheem 1, BARI Sheem 6, IPSA Sheem 2, Rupban",
                    MajorDistricts = "Chattogram, Cox's Bazar, Cumilla, Jessore, Mymensingh",
                    Division = "All",
                    KeyTips = "Provide sturdy bamboo macha/trellis. Control aphids and pod borers at early flowering stage.",
                    IconClass = "bi-flower3",
                    BadgeColor = "info"
                },
                new()
                {
                    Name = "Watermelon (Coastal & Char)",
                    BanglaName = "তরমুজ (উপকূলীয় ও চর)",
                    ScientificName = "Citrullus lanatus",
                    Category = "Fruits",
                    Season = "Rabi",
                    ProfileCropName = "Fruits",
                    SowingMonths = new List<int> { 12, 1 },
                    GrowingMonths = new List<int> { 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4, 5 },
                    DurationDays = "80 – 95 Days",
                    OptimalTemperature = "24°C – 35°C (Warm sun raises sugar content)",
                    SoilTypes = "Sandy Loam, River Char lands (বেলে-দোআঁশ ও নদীর চর)",
                    WaterRequirement = "Medium — pit method with localized basin watering; avoid flooding vines",
                    PopularVarieties = "Dragon, Black Diamond, Pakiza, Sweet Miracle, Big Top",
                    MajorDistricts = "Patuakhali (Galachipa), Bhola, Barisal, Barguna, Noakhali, Natore",
                    Division = "Barisal, Chittagong, Khulna",
                    KeyTips = "Major coastal cash crop. Lay dry straw under developing melons to prevent soil dampness and spot marks.",
                    IconClass = "bi-heart-fill",
                    BadgeColor = "success"
                },
                new()
                {
                    Name = "Sugarcane (Commercial Annual)",
                    BanglaName = "আখ (বার্ষিক অর্থকরী ফসল)",
                    ScientificName = "Saccharum officinarum",
                    Category = "CashCrops",
                    Season = "YearRound",
                    ProfileCropName = "Other",
                    SowingMonths = new List<int> { 10, 11, 2, 3 },
                    GrowingMonths = new List<int> { 12, 1, 4, 5, 6, 7, 8, 9, 10 },
                    HarvestingMonths = new List<int> { 11, 12, 1, 2, 3 },
                    DurationDays = "300 – 360 Days (10-12 Months)",
                    OptimalTemperature = "26°C – 38°C",
                    SoilTypes = "Deep fertile Loam, Silt Loam (গভীর উর্বর দোআঁশ)",
                    WaterRequirement = "High — long duration crop with multiple trench irrigations",
                    PopularVarieties = "Isd 37, Isd 39, Isd 40, BSRI Akh 42, BSRI Akh 45",
                    MajorDistricts = "Joypurhat, Kushtia, Chuadanga, Natore, Faridpur, Thakurgaon",
                    Division = "Rajshahi, Khulna, Rangpur",
                    KeyTips = "Tie canes together in clumps to prevent storm lodging. Intercrop with mustard, potato, or onion in first 90 days.",
                    IconClass = "bi-tree-fill",
                    BadgeColor = "success"
                },
                new()
                {
                    Name = "Sunflower (Saline-Tolerant Oilseed)",
                    BanglaName = "সূর্যমুখী (লবণাক্ততা সহনশীল)",
                    ScientificName = "Helianthus annuus",
                    Category = "Oilseeds",
                    Season = "Rabi",
                    ProfileCropName = "Oilseeds",
                    SowingMonths = new List<int> { 11, 12 },
                    GrowingMonths = new List<int> { 12, 1, 2 },
                    HarvestingMonths = new List<int> { 3, 4 },
                    DurationDays = "90 – 105 Days",
                    OptimalTemperature = "20°C – 28°C",
                    SoilTypes = "Sandy Loam, Coastal Silt with mild salinity (উপকূলীয় বেলে-দোআঁশ)",
                    WaterRequirement = "Low to Medium — 2 light irrigations; drought and moderate salt tolerant",
                    PopularVarieties = "BARI Surjomukhi 2, BARI Surjomukhi 3, Hysun 33, Pacific 298",
                    MajorDistricts = "Patuakhali, Barguna, Bhola, Satkhira, Khulna, Rajshahi",
                    Division = "Barisal, Khulna",
                    KeyTips = "Excellent crop for southern coastal belt where high salinity restricts winter Boro rice.",
                    IconClass = "bi-brightness-high-fill",
                    BadgeColor = "warning"
                },
                new()
                {
                    Name = "Groundnut / Peanut (Char & Sandy Soils)",
                    BanglaName = "চীনাবাদাম (চর ও বেলে দোআঁশ)",
                    ScientificName = "Arachis hypogaea",
                    Category = "Oilseeds",
                    Season = "Rabi",
                    ProfileCropName = "Oilseeds",
                    SowingMonths = new List<int> { 11, 12, 5 },
                    GrowingMonths = new List<int> { 12, 1, 2, 6, 7 },
                    HarvestingMonths = new List<int> { 3, 4, 9 },
                    DurationDays = "120 – 140 Days",
                    OptimalTemperature = "22°C – 32°C",
                    SoilTypes = "Sandy Loam, River Char sandbeds (বেলে ও চরের বেলে-দোআঁশ)",
                    WaterRequirement = "Low — thrives on riverbank sands",
                    PopularVarieties = "BARI Chinabadam 8, BARI Chinabadam 9, Dhaka-1, Bina Chinabadam 4",
                    MajorDistricts = "Kishoreganj, Jamalpur, Sirajganj, Tangail, Faridpur, Noakhali (Char)",
                    Division = "All",
                    KeyTips = "Apply Gypsum (Sulphur & Calcium) at 30 days for hard, full kernel shells. Harvest when inner shell turns dark.",
                    IconClass = "bi-nut-fill",
                    BadgeColor = "warning"
                }
            };

            await db.CropCalendarEntries.AddRangeAsync(entries);
            await db.SaveChangesAsync();
        }
    }
}
