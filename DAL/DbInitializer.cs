using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
        }

        private static async Task SeedDemoDataAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            var farmer = await CreateUserAsync(userManager, "farmer@krishilink.com", "01711000001", "Rahim Uddin", AppRoles.Farmer, "Shibganj, Bogra", "Rice (Boro)", "Uddin Agro Farm");
            var karim = await CreateUserAsync(userManager, "karim.mia@krishilink.com", "01711000002", "Karim Mia", AppRoles.Farmer, "Sherpur, Bogra", "Potato");
            var fatema = await CreateUserAsync(userManager, "fatema@krishilink.com", "01711000003", "Fatema Begum", AppRoles.Farmer, "Gabtali, Bogra", "Vegetables");
            var salma = await CreateUserAsync(userManager, "salma@krishilink.com", "01711000004", "Salma Akter", AppRoles.Farmer, "Bochaganj, Dinajpur", "Rice (Aman)");
            var motaleb = await CreateUserAsync(userManager, "motaleb@krishilink.com", "01711000005", "Motaleb Hossain", AppRoles.Farmer, "Birol, Dinajpur", "Wheat");

            var eqOwner = await CreateUserAsync(userManager, "equipment@krishilink.com", "01712000001", "Abdul Karim", AppRoles.EquipmentOwner, "Bogra Sadar, Bogra", "Tractor", "Karim Agro Machinery");
            var gdOwner = await CreateUserAsync(userManager, "godown@krishilink.com", "01713000001", "Abdul Mannan", AppRoles.GodownOwner, "Dinajpur Sadar, Dinajpur", "Cold Storage", "Green Grain Storage Ltd.");

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
            // so the demo owners have a visible pending balance.
            var eqRepo = new EquipmentRevenueRepository(db);
            var eqCompleted = eqRepo.GetBookings(eqOwner.Id).Where(b => b.Status == BookingStatus.Completed).OrderBy(b => b.EndDate).ToList();
            Settle(eqRepo, eqOwner, eqCompleted.Take(2), "bKash", -95, "Completed");
            Settle(eqRepo, eqOwner, eqCompleted.Skip(2).Take(1), "Nagad", -35, "Completed");
            Settle(eqRepo, eqOwner, eqCompleted.Skip(3).Take(1), "bKash", -3, "Processing");

            var gdRepo = new GodownRevenueRepository(db);
            var gdCompleted = gdRepo.GetBookings(gdOwner.Id).Where(b => b.Status == BookingStatus.Completed).OrderBy(b => b.EndDate).ToList();
            Settle(gdRepo, gdOwner, gdCompleted.Take(1), "Bank Transfer", -60, "Completed");
            Settle(gdRepo, gdOwner, gdCompleted.Skip(1).Take(1), "bKash", -2, "Processing");

            var completedRental = await db.EquipmentBookings.FirstAsync(b => b.EquipmentId == equipment[1].Id && b.Status == BookingStatus.Completed);
            var completedStorage = await db.GodownBookings.FirstAsync(b => b.GodownId == godowns[1].Id && b.Status == BookingStatus.Completed);
            db.BookingExpenses.AddRange(
                new BookingExpense { BookingType = "Equipment", BookingId = completedRental.Id, OwnerId = eqOwner.Id, Amount = 2500, Note = "Diesel for harvester", RecordedOn = today.AddDays(-113) },
                new BookingExpense { BookingType = "Godown", BookingId = completedStorage.Id, OwnerId = gdOwner.Id, Amount = 4500, Note = "Fumigation before intake", RecordedOn = today.AddDays(-149) });
            await db.SaveChangesAsync();
        }

        private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string email, string phone,
            string fullName, string role, string location, string specialization, string? business = null)
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
                OnboardingCompletedAt = DateTime.UtcNow.AddMonths(-8),
                CreatedAt = DateTime.UtcNow.AddMonths(-8)
            };

            var result = await userManager.CreateAsync(user, DemoCredential);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Seeding user '{email}' failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");

            await userManager.AddToRoleAsync(user, role);
            return user;
        }

        private static Equipment Machine(string name, string category, decimal daily, decimal hourly, string location, string description, string image, int createdDaysAgo) =>
            new()
            {
                Name = name,
                Category = category,
                DailyRate = daily,
                HourlyRate = hourly,
                Location = location,
                Description = description,
                ImageUrls = image,
                CreatedAt = DateTime.UtcNow.AddDays(createdDaysAgo)
            };

        private static Godown Storage(string name, string type, double tons, decimal price, string location, string description, string facilities, string image, int createdDaysAgo) =>
            new()
            {
                Name = name,
                StorageType = type,
                CapacityInTons = tons,
                PricePerTonPerMonth = price,
                Location = location,
                Description = description,
                Facilities = facilities,
                ImageUrls = image,
                CreatedAt = DateTime.UtcNow.AddDays(createdDaysAgo)
            };

        private static EquipmentBooking Rental(Equipment e, ApplicationUser farmer, int startOffset, int endOffset, string status, int requestedOffset, string? note = null, string? reject = null) =>
            new()
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

        private static GodownBooking StorageBooking(Godown g, ApplicationUser farmer, double tons, int startOffset, int endOffset, string status, int requestedOffset, string? note = null, string? reject = null) =>
            new()
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

        /// <summary>Creates one payout covering the given bookings (5% commission) and links them to it.</summary>
        private static void Settle(IOwnerRevenueRepository repo, ApplicationUser owner, IEnumerable<RevenueBooking> bookings, string method, int daysAgo, string status)
        {
            var list = bookings.ToList();
            if (list.Count == 0) return;

            var gross = list.Sum(b => b.Gross);
            var commission = list.Sum(b => decimal.Round(b.Gross * 0.05m, 0));
            var date = DateTime.Today.AddDays(daysAgo);
            var payoutId = repo.AddPayout(new Transaction
            {
                UserId = owner.Id,
                Reference = $"KL-PO-{date:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                GrossAmount = gross,
                Commission = commission,
                Amount = gross - commission,
                PaymentMethod = method,
                PayoutAccount = method == "Bank Transfer" ? "0123456789012" : owner.PhoneNumber,
                Status = status,
                TransactionDate = date
            });
            repo.MarkBookingsPaid(list.Select(b => b.Id), payoutId);
        }
    }
}
