using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    public interface IGodownService
    {
        // Farmer / public
        Task<GodownBrowseViewModel> BrowseAsync(GodownSearchCriteria criteria);
        Task<GodownDetailViewModel?> GetDetailsAsync(int id);

        /// <summary>Creates a pending storage request. Returns a user-facing error message, or null on success.</summary>
        Task<string?> RequestStorageAsync(string farmerId, GodownDetailViewModel request);

        // Owner
        Task<GodownOwnerDashboardViewModel> GetOwnerDashboardAsync(string ownerId);
        Task<GodownBookingRequestsViewModel> GetOwnerRequestsAsync(string ownerId);
        Task<int> CountPendingAsync(string ownerId);

        /// <summary>
        /// Applies an owner decision. Accepting is refused when the request would exceed free capacity for its dates
        /// or touches a blocked date; on success overlapping pending requests that no longer fit are auto-rejected.
        /// </summary>
        Task<DecisionResult> RespondAsync(string ownerId, int bookingId, string decision, string? reason);
        Task<GodownListingViewModel?> GetListingAsync(string ownerId, int id);
        Task<bool> SaveListingAsync(string ownerId, GodownListingViewModel model);
        Task<ManageAvailabilityViewModel?> GetAvailabilityAsync(string ownerId, int godownId, DateTime? month);
        Task<bool> SaveAvailabilityAsync(string ownerId, int godownId, DateTime month, IEnumerable<DateTime> blockedDates);
    }

    public class GodownService : IGodownService
    {
        private const string UploadFolder = "godowns";
        private const int CalendarHorizonMonths = 3;
        private readonly IRepository<Godown> _godowns;
        private readonly IRepository<GodownBooking> _bookings;
        private readonly IRepository<GodownBlockedDate> _blockedDates;
        private readonly IFileStorageService _files;
        private readonly IReviewService _reviews;

        public GodownService(IRepository<Godown> godowns, IRepository<GodownBooking> bookings,
            IRepository<GodownBlockedDate> blockedDates, IFileStorageService files, IReviewService reviews)
        {
            _godowns = godowns;
            _bookings = bookings;
            _blockedDates = blockedDates;
            _files = files;
            _reviews = reviews;
        }

        // ---------------------------------------------------------------- Browse & details

        public async Task<GodownBrowseViewModel> BrowseAsync(GodownSearchCriteria c)
        {
            var today = DateTime.Today;
            var query = _godowns.Query().Where(g => g.IsActive);

            if (!string.IsNullOrWhiteSpace(c.SearchTerm))
            {
                var term = c.SearchTerm.Trim();
                query = query.Where(g => g.Name.Contains(term) || g.StorageType.Contains(term)
                    || g.Location.Contains(term) || g.Owner!.FullName.Contains(term));
            }
            if (c.SelectedStorageTypes is { Count: > 0 })
                query = query.Where(g => c.SelectedStorageTypes.Contains(g.StorageType));
            if (!string.IsNullOrWhiteSpace(c.Location))
                query = query.Where(g => g.Location.Contains(c.Location.Trim()));
            if (c.SelectedMaxPrice.HasValue)
                query = query.Where(g => g.PricePerTonPerMonth <= c.SelectedMaxPrice.Value);

            // Occupancy is measured for the requested window when given, otherwise for today.
            var windowStart = (c.AvailableStartDate ?? today).Date;
            var windowEnd = (c.AvailableEndDate ?? windowStart).Date;
            if (windowEnd < windowStart) windowEnd = windowStart;
            if (c.AvailableStartDate.HasValue || c.AvailableEndDate.HasValue)
                query = query.Where(g => !g.BlockedDates.Any(d => d.Date >= windowStart && d.Date <= windowEnd));

            var rows = await query.Select(g => new
            {
                g.Id,
                g.Name,
                g.StorageType,
                g.Location,
                g.CapacityInTons,
                g.PricePerTonPerMonth,
                g.ImageUrls,
                g.Facilities,
                g.AverageRating,
                g.ReviewCount,
                g.CreatedAt,
                OwnerName = g.Owner!.FullName,
                Occupied = g.Bookings
                    .Where(b => b.Status == BookingStatus.Accepted && b.StartDate <= windowEnd && windowStart <= b.EndDate)
                    .Sum(b => (double?)b.StorageTons) ?? 0
            }).ToListAsync();

            IEnumerable<GodownItemViewModel> items = rows.Select(g => new GodownItemViewModel
            {
                Id = g.Id,
                Name = g.Name,
                StorageType = g.StorageType,
                Location = g.Location,
                TotalCapacityTons = g.CapacityInTons,
                AvailableCapacityTons = Math.Max(0, g.CapacityInTons - g.Occupied),
                PricePerTonPerMonth = g.PricePerTonPerMonth,
                ImageUrl = ListingFormat.Split(g.ImageUrls).FirstOrDefault() ?? string.Empty,
                OwnerName = g.OwnerName,
                Rating = g.AverageRating,
                ReviewCount = g.ReviewCount,
                Facilities = ListingFormat.Split(g.Facilities),
                CreatedAt = g.CreatedAt
            });

            if (c.SelectedMinCapacity is > 0)
                items = items.Where(g => g.AvailableCapacityTons >= c.SelectedMinCapacity.Value);
            if (c.AvailableStartDate.HasValue || c.AvailableEndDate.HasValue)
                items = items.Where(g => g.IsAvailable);

            var sort = (c.SortBy ?? "newest").ToLowerInvariant();
            items = sort switch
            {
                "price_asc" => items.OrderBy(g => g.PricePerTonPerMonth),
                "price_desc" => items.OrderByDescending(g => g.PricePerTonPerMonth),
                "capacity_desc" => items.OrderByDescending(g => g.AvailableCapacityTons),
                "distance" => items.OrderBy(g => g.Location).ThenByDescending(g => g.CreatedAt),
                _ => items.OrderByDescending(g => g.CreatedAt)
            };

            var model = new GodownBrowseViewModel
            {
                SearchTerm = c.SearchTerm,
                SelectedStorageTypes = c.SelectedStorageTypes ?? new List<string>(),
                Location = c.Location,
                SelectedMinCapacity = c.SelectedMinCapacity,
                SelectedMaxPrice = c.SelectedMaxPrice ?? 2500,
                AvailableStartDate = c.AvailableStartDate,
                AvailableEndDate = c.AvailableEndDate,
                SortBy = sort,
                GodownList = items.ToList()
            };

            var types = await _godowns.Query().Where(g => g.IsActive).Select(g => g.StorageType).Distinct().OrderBy(t => t).ToListAsync();
            if (types.Count > 0) model.AvailableStorageTypes = types;
            var locations = await _godowns.Query().Where(g => g.IsActive).Select(g => g.Location).Distinct().OrderBy(l => l).ToListAsync();
            if (locations.Count > 0) model.AvailableLocations = locations;
            return model;
        }

        public async Task<GodownDetailViewModel?> GetDetailsAsync(int id)
        {
            var g = await _godowns.Query().Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == id);
            if (g is null) return null;

            var available = Math.Max(0, g.CapacityInTons - await OccupiedTonsAsync(id, DateTime.Today, DateTime.Today));

            var from = DateTime.Today;
            var to = from.AddMonths(CalendarHorizonMonths);
            var accepted = await _bookings.Query()
                .Where(b => b.GodownId == id && b.Status == BookingStatus.Accepted && b.EndDate >= from && b.StartDate <= to)
                .Select(b => new { b.StartDate, b.EndDate, b.StorageTons })
                .ToListAsync();
            var blocked = await _blockedDates.Query()
                .Where(d => d.GodownId == id && d.Date >= from && d.Date <= to)
                .Select(d => d.Date)
                .ToListAsync();
            var fullDays = EachDay(from, to)
                .Where(day => accepted.Where(b => b.StartDate <= day && day <= b.EndDate).Sum(b => b.StorageTons) >= g.CapacityInTons);
            var reviewsList = await _reviews.GetReviewsForGodownAsync(id);

            return new GodownDetailViewModel
            {
                Id = g.Id,
                Name = g.Name,
                StorageType = g.StorageType,
                Location = g.Location,
                Description = g.Description,
                TotalCapacityTons = g.CapacityInTons,
                AvailableCapacityTons = available,
                UnavailableDates = blocked.Concat(fullDays).Distinct().OrderBy(d => d).ToList(),
                PricePerTonPerMonth = $"{ListingFormat.Taka(g.PricePerTonPerMonth)} / Ton / Month",
                DailyRatePerTon = $"{ListingFormat.Taka(Math.Round(g.PricePerTonPerMonth / 30m, 2))} / Ton / Day",
                Status = !g.IsActive ? "Inactive" : available > 0 ? "Available" : "Fully Booked",
                OwnerName = g.Owner?.FullName ?? string.Empty,
                OwnerPhone = g.Owner?.PhoneNumber ?? string.Empty,
                OwnerMemberSince = ListingFormat.MemberSince(g.Owner?.CreatedAt ?? g.CreatedAt),
                OwnerRating = g.AverageRating,
                TotalReviews = g.ReviewCount,
                AverageRating = g.AverageRating,
                ReviewCount = g.ReviewCount,
                ImageUrls = ListingFormat.Split(g.ImageUrls),
                Facilities = ListingFormat.Split(g.Facilities),
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddMonths(1),
                RequestedCapacityTons = Math.Min(10, available),
                Reviews = reviewsList
            };
        }

        public async Task<string?> RequestStorageAsync(string farmerId, GodownDetailViewModel r)
        {
            if (r.StartDate is null || r.EndDate is null) return "Please choose a start and end date.";
            var s = r.StartDate.Value.Date;
            var t = r.EndDate.Value.Date;
            if (s < DateTime.Today) return "Start date cannot be in the past.";
            if (t <= s) return "End date must be after the start date.";
            if (r.RequestedCapacityTons <= 0) return "Requested capacity must be greater than zero.";

            var g = await _godowns.Query().FirstOrDefaultAsync(x => x.Id == r.Id);
            if (g is null) return "This storage facility no longer exists.";
            if (!g.IsActive) return "This storage facility is not accepting bookings right now.";
            if (g.OwnerId == farmerId) return "You cannot book your own storage facility.";

            var clash = await FindConflictAsync(g, r.RequestedCapacityTons, s, t);
            if (clash is not null) return clash;

            await _bookings.AddAsync(new GodownBooking
            {
                GodownId = g.Id,
                FarmerId = farmerId,
                StorageTons = r.RequestedCapacityTons,
                StartDate = s,
                EndDate = t,
                Note = string.IsNullOrWhiteSpace(r.BookingNotes) ? null : r.BookingNotes.Trim(),
                Status = BookingStatus.Pending,
                RequestedOn = DateTime.Now
            });
            await _bookings.SaveChangesAsync();
            return null;
        }

        // ---------------------------------------------------------------- Owner

        public async Task<GodownOwnerDashboardViewModel> GetOwnerDashboardAsync(string ownerId)
        {
            var godowns = await OwnerGodownsAsync(ownerId);
            var pending = await OwnerBookingsQuery(ownerId).Where(b => b.Status == BookingStatus.Pending).ToListAsync();

            return new GodownOwnerDashboardViewModel
            {
                TotalGodowns = godowns.Count,
                TotalCapacityTons = godowns.Sum(g => g.TotalCapacityTons),
                OccupiedCapacityTons = godowns.Sum(g => g.OccupiedTons),
                Godowns = godowns,
                PendingRequestItems = pending.Select(ToRequestItem).OrderByDescending(r => r.RequestedOn).ToList()
            };
        }

        public async Task<GodownBookingRequestsViewModel> GetOwnerRequestsAsync(string ownerId)
        {
            var bookings = await OwnerBookingsQuery(ownerId).ToListAsync();
            return new GodownBookingRequestsViewModel
            {
                Requests = bookings.Select(ToRequestItem).OrderByDescending(r => r.RequestedOn).ToList(),
                Godowns = await OwnerGodownsAsync(ownerId)
            };
        }

        public Task<int> CountPendingAsync(string ownerId) =>
            _bookings.Query().CountAsync(b => b.Godown!.OwnerId == ownerId && b.Status == BookingStatus.Pending);

        public async Task<DecisionResult> RespondAsync(string ownerId, int bookingId, string decision, string? reason)
        {
            var booking = await _bookings.QueryTracked().Include(b => b.Godown)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.Godown!.OwnerId == ownerId);
            if (booking is null) return DecisionResult.Fail("This booking request could not be found.");

            var next = BookingWorkflow.Next(booking.Status, decision);
            if (next is null) return DecisionResult.Fail($"A {booking.Status.ToLowerInvariant()} request cannot be {decision.ToLowerInvariant()}ed.");

            var autoRejected = new List<int>();
            if (next == BookingStatus.Accepted)
            {
                var godown = booking.Godown!;
                var clash = await FindConflictAsync(godown, booking.StorageTons, booking.StartDate, booking.EndDate, excludeBookingId: booking.Id);
                if (clash is not null) return DecisionResult.Fail(clash);

                // Overlapping pending requests that no longer fit once this one occupies capacity are declined with a reason.
                var overlapping = await _bookings.QueryTracked()
                    .Where(b => b.GodownId == booking.GodownId && b.Id != booking.Id && b.Status == BookingStatus.Pending
                        && b.StartDate <= booking.EndDate && booking.StartDate <= b.EndDate)
                    .ToListAsync();
                foreach (var other in overlapping)
                {
                    var occupied = await OccupiedTonsAsync(godown.Id, other.StartDate, other.EndDate) + booking.StorageTons;
                    if (occupied + other.StorageTons <= godown.CapacityInTons) continue;
                    other.Status = BookingStatus.Rejected;
                    other.RejectReason = BookingWorkflow.AutoRejectReason;
                    other.UpdatedOn = DateTime.Now;
                    autoRejected.Add(other.Id);
                }
            }

            booking.Status = next;
            booking.RejectReason = next == BookingStatus.Rejected && !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : null;
            booking.UpdatedOn = DateTime.Now;
            await _bookings.SaveChangesAsync();
            return DecisionResult.Ok(autoRejected);
        }

        public async Task<GodownListingViewModel?> GetListingAsync(string ownerId, int id)
        {
            var g = await _godowns.Query().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (g is null) return null;

            return new GodownListingViewModel
            {
                Id = g.Id,
                Name = g.Name,
                Category = g.StorageType,
                Location = g.Location,
                TotalCapacity = g.CapacityInTons,
                CapacityUnit = "Tons",
                AvailableCapacity = Math.Max(0, g.CapacityInTons - await OccupiedTonsAsync(g.Id, DateTime.Today, DateTime.Today)),
                PriceAmount = g.PricePerTonPerMonth,
                PricePeriod = "Month",
                Description = g.Description,
                IsAvailable = g.IsActive,
                SelectedFacilities = ListingFormat.Split(g.Facilities),
                ExistingImageUrls = ListingFormat.Split(g.ImageUrls)
            };
        }

        public async Task<bool> SaveListingAsync(string ownerId, GodownListingViewModel model)
        {
            Godown entity;
            if (model.IsEditMode)
            {
                var existing = await _godowns.QueryTracked().FirstOrDefaultAsync(x => x.Id == model.Id && x.OwnerId == ownerId);
                if (existing is null) return false;
                entity = existing;
            }
            else
            {
                entity = new Godown { OwnerId = ownerId, CreatedAt = DateTime.UtcNow };
                await _godowns.AddAsync(entity);
            }

            var images = model.ExistingImageUrls.Concat(await _files.SaveImagesAsync(model.ImageFiles, UploadFolder));

            entity.Name = model.Name.Trim();
            entity.StorageType = model.Category.Trim();
            entity.Location = model.Location.Trim();
            entity.Description = model.Description.Trim();
            entity.CapacityInTons = ToTons(model.TotalCapacity, model.CapacityUnit);
            entity.PricePerTonPerMonth = model.PricePeriod == "Day" ? model.PriceAmount * 30 : model.PriceAmount;
            entity.IsActive = model.IsAvailable;
            entity.Facilities = ListingFormat.Join(model.SelectedFacilities);
            entity.ImageUrls = ListingFormat.Join(images);

            await _godowns.SaveChangesAsync();
            model.Id = entity.Id;
            return true;
        }

        public async Task<ManageAvailabilityViewModel?> GetAvailabilityAsync(string ownerId, int godownId, DateTime? month)
        {
            var g = await _godowns.Query().FirstOrDefaultAsync(x => x.Id == godownId && x.OwnerId == ownerId);
            if (g is null) return null;

            var first = new DateTime((month ?? DateTime.Today).Year, (month ?? DateTime.Today).Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            return new ManageAvailabilityViewModel
            {
                ListingId = g.Id,
                ListingName = g.Name,
                Category = g.StorageType,
                RateText = $"{ListingFormat.Taka(g.PricePerTonPerMonth)} / Ton / Month",
                Location = g.Location,
                ThumbnailUrl = ListingFormat.Split(g.ImageUrls).FirstOrDefault() ?? string.Empty,
                Month = first,
                MonthName = first.ToString("MMMM yyyy"),
                FarmerBookedDates = (await StoredDaysAsync(godownId, first, last)).ToList(),
                OwnerBlockedDates = await _blockedDates.Query()
                    .Where(d => d.GodownId == godownId && d.Date >= first && d.Date <= last)
                    .Select(d => d.Date)
                    .ToListAsync()
            };
        }

        public async Task<bool> SaveAvailabilityAsync(string ownerId, int godownId, DateTime month, IEnumerable<DateTime> blockedDates)
        {
            if (!await _godowns.Query().AnyAsync(x => x.Id == godownId && x.OwnerId == ownerId)) return false;

            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            _blockedDates.RemoveRange(await _blockedDates.QueryTracked()
                .Where(d => d.GodownId == godownId && d.Date >= first && d.Date <= last)
                .ToListAsync());

            // Days with goods already stored cannot be closed by the owner
            var stored = await StoredDaysAsync(godownId, first, last);
            foreach (var date in blockedDates.Select(d => d.Date).Distinct().Where(d => d >= first && d <= last && !stored.Contains(d)))
                await _blockedDates.AddAsync(new GodownBlockedDate { GodownId = godownId, Date = date });

            await _blockedDates.SaveChangesAsync();
            return true;
        }

        // ---------------------------------------------------------------- Helpers

        /// <summary>
        /// The single source of truth for godown over-booking: a blocked date inside [start, end], or requested tons
        /// exceeding the capacity left after accepted bookings in that window. Returns a message, or null when it fits.
        /// </summary>
        private async Task<string?> FindConflictAsync(Godown g, double tons, DateTime start, DateTime end, int? excludeBookingId = null)
        {
            var blocked = await _blockedDates.Query()
                .Where(d => d.GodownId == g.Id && d.Date >= start && d.Date <= end)
                .OrderBy(d => d.Date)
                .Select(d => (DateTime?)d.Date)
                .FirstOrDefaultAsync();
            if (blocked is not null)
                return $"The owner has closed this godown on {blocked:dd MMM yyyy}. Please choose different dates.";

            var free = g.CapacityInTons - await OccupiedTonsAsync(g.Id, start, end, excludeBookingId);
            return tons <= free
                ? null
                : $"Only {Math.Max(0, free):N0} tons are free for the selected period. Please reduce the quantity or change the dates.";
        }

        private async Task<HashSet<DateTime>> StoredDaysAsync(int godownId, DateTime from, DateTime to)
        {
            var accepted = await _bookings.Query()
                .Where(b => b.GodownId == godownId && b.Status == BookingStatus.Accepted && b.EndDate >= from && b.StartDate <= to)
                .Select(b => new { b.StartDate, b.EndDate })
                .ToListAsync();
            return accepted.SelectMany(b => EachDay(b.StartDate, b.EndDate)).Where(d => d >= from && d <= to).ToHashSet();
        }

        private static IEnumerable<DateTime> EachDay(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1)) yield return d;
        }

        private static double ToTons(double quantity, string unit) => unit switch
        {
            "Maunds" => quantity * 0.04,
            "Bags" => quantity * 0.05,
            "Quintals" => quantity * 0.1,
            _ => quantity
        };

        private async Task<double> OccupiedTonsAsync(int godownId, DateTime from, DateTime to, int? excludeBookingId = null) =>
            await _bookings.Query()
                .Where(b => b.GodownId == godownId && b.Id != excludeBookingId && b.Status == BookingStatus.Accepted && b.StartDate <= to && from <= b.EndDate)
                .SumAsync(b => (double?)b.StorageTons) ?? 0;

        private async Task<List<OwnerGodownItem>> OwnerGodownsAsync(string ownerId)
        {
            var today = DateTime.Today;
            var rows = await _godowns.Query()
                .Where(g => g.OwnerId == ownerId)
                .OrderByDescending(g => g.CreatedAt)
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.StorageType,
                    g.CapacityInTons,
                    g.IsActive,
                    Occupied = g.Bookings.Where(b => b.Status == BookingStatus.Accepted && b.StartDate <= today && today <= b.EndDate)
                        .Sum(b => (double?)b.StorageTons) ?? 0
                })
                .ToListAsync();

            return rows.Select(g =>
            {
                var available = Math.Max(0, g.CapacityInTons - g.Occupied);
                return new OwnerGodownItem
                {
                    Id = g.Id,
                    Name = g.Name,
                    StorageType = g.StorageType,
                    TotalCapacityTons = g.CapacityInTons,
                    AvailableCapacityTons = available,
                    Status = !g.IsActive ? "Inactive" : available <= 0 ? "Full" : "Active"
                };
            }).ToList();
        }

        private IQueryable<GodownBooking> OwnerBookingsQuery(string ownerId) =>
            _bookings.Query()
                .Include(b => b.Godown)
                .Include(b => b.Farmer)
                .Where(b => b.Godown!.OwnerId == ownerId);

        private static GodownBookingRequestItem ToRequestItem(GodownBooking b) => new()
        {
            Id = b.Id,
            FarmerName = string.IsNullOrWhiteSpace(b.Farmer?.FullName) ? "Farmer" : b.Farmer!.FullName,
            GodownId = b.GodownId,
            GodownName = b.Godown?.Name ?? string.Empty,
            RequestedCapacityTons = b.StorageTons,
            DateRange = ListingFormat.DateRange(b.StartDate, b.EndDate),
            Note = b.Note,
            Status = b.Status,
            RejectReason = b.RejectReason,
            RequestedOn = b.RequestedOn
        };
    }
}
