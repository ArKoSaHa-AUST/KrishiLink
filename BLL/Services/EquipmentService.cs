using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    public interface IEquipmentService
    {
        // Farmer / public
        Task<EquipmentBrowseViewModel> BrowseAsync(EquipmentSearchCriteria criteria);
        Task<EquipmentDetailViewModel?> GetDetailsAsync(int id);

        /// <summary>Creates a pending rental request. Returns a user-facing error message, or null on success.</summary>
        Task<string?> RequestRentalAsync(string farmerId, int equipmentId, DateTime? start, DateTime? end, string? note);

        // Owner
        Task<EquipmentOwnerDashboardViewModel> GetOwnerDashboardAsync(string ownerId);
        Task<RentalRequestsViewModel> GetOwnerRequestsAsync(string ownerId);
        Task<int> CountPendingAsync(string ownerId);

        /// <summary>
        /// Applies an owner decision. Accepting is refused when the dates clash with an already accepted rental or a
        /// blocked date; on success every other pending request that overlaps the accepted one is auto-rejected.
        /// </summary>
        Task<DecisionResult> RespondAsync(string ownerId, int bookingId, string decision, string? reason);
        Task<EquipmentListingViewModel?> GetListingAsync(string ownerId, int id);
        Task<bool> SaveListingAsync(string ownerId, EquipmentListingViewModel model);
        Task<ManageAvailabilityViewModel?> GetAvailabilityAsync(string ownerId, int equipmentId, DateTime? month);
        Task<bool> SaveAvailabilityAsync(string ownerId, int equipmentId, DateTime month, IEnumerable<DateTime> blockedDates);
    }

    public class EquipmentService : IEquipmentService
    {
        private const string UploadFolder = "equipment";
        private readonly IRepository<Equipment> _equipment;
        private readonly IRepository<EquipmentBooking> _bookings;
        private readonly IRepository<EquipmentBlockedDate> _blockedDates;
        private readonly IFileStorageService _files;
        private readonly IReviewService _reviews;

        public EquipmentService(IRepository<Equipment> equipment, IRepository<EquipmentBooking> bookings,
            IRepository<EquipmentBlockedDate> blockedDates, IFileStorageService files, IReviewService reviews)
        {
            _equipment = equipment;
            _bookings = bookings;
            _blockedDates = blockedDates;
            _files = files;
            _reviews = reviews;
        }

        // ---------------------------------------------------------------- Browse & details

        public async Task<EquipmentBrowseViewModel> BrowseAsync(EquipmentSearchCriteria c)
        {
            var query = _equipment.Query().Include(e => e.Owner).AsQueryable();

            if (!string.IsNullOrWhiteSpace(c.SearchTerm))
            {
                var term = c.SearchTerm.Trim();
                query = query.Where(e => e.Name.Contains(term) || e.Category.Contains(term)
                    || e.Location.Contains(term) || e.Owner!.FullName.Contains(term));
            }
            if (c.SelectedCategories is { Count: > 0 })
                query = query.Where(e => c.SelectedCategories.Contains(e.Category));
            if (!string.IsNullOrWhiteSpace(c.Location))
                query = query.Where(e => e.Location.Contains(c.Location.Trim()));
            if (c.SelectedMaxPrice.HasValue)
                query = query.Where(e => e.DailyRate <= c.SelectedMaxPrice.Value);
            if (c.AvailabilityDate.HasValue)
            {
                var day = c.AvailabilityDate.Value.Date;
                query = query.Where(e => e.IsAvailable
                    && !e.BlockedDates.Any(d => d.Date == day)
                    && !e.Bookings.Any(b => b.Status == BookingStatus.Accepted && b.StartDate <= day && day <= b.EndDate));
            }

            var sort = (c.SortBy ?? "newest").ToLowerInvariant();
            query = sort switch
            {
                "price_asc" => query.OrderBy(e => e.DailyRate),
                "price_desc" => query.OrderByDescending(e => e.DailyRate),
                "distance" => query.OrderBy(e => e.Location).ThenByDescending(e => e.CreatedAt),
                _ => query.OrderByDescending(e => e.CreatedAt)
            };

            var items = await query.Select(e => new EquipmentItemViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Category = e.Category,
                DailyRate = e.DailyRate,
                HourlyRate = e.HourlyRate,
                Location = e.Location,
                IsAvailable = e.IsAvailable,
                ImageUrl = e.ImageUrls,
                OwnerName = e.Owner!.FullName,
                Rating = e.AverageRating,
                ReviewCount = e.ReviewCount,
                CreatedAt = e.CreatedAt
            }).ToListAsync();
            items.ForEach(i => i.ImageUrl = ListingFormat.Split(i.ImageUrl).FirstOrDefault() ?? string.Empty);

            var model = new EquipmentBrowseViewModel
            {
                SearchTerm = c.SearchTerm,
                SelectedCategories = c.SelectedCategories ?? new List<string>(),
                Location = c.Location,
                SelectedMaxPrice = c.SelectedMaxPrice ?? 5000,
                AvailabilityDate = c.AvailabilityDate,
                SortBy = sort,
                EquipmentList = items
            };

            var locations = await _equipment.Query().Select(e => e.Location).Distinct().OrderBy(l => l).ToListAsync();
            if (locations.Count > 0) model.AvailableLocations = locations;
            return model;
        }

        public async Task<EquipmentDetailViewModel?> GetDetailsAsync(int id)
        {
            var e = await _equipment.Query().Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == id);
            if (e is null) return null;

            var from = DateTime.Today;
            var to = from.AddMonths(3);
            var accepted = await _bookings.Query()
                .Where(b => b.EquipmentId == id && b.Status == BookingStatus.Accepted && b.EndDate >= from && b.StartDate <= to)
                .Select(b => new { b.StartDate, b.EndDate })
                .ToListAsync();
            var blocked = await _blockedDates.Query()
                .Where(d => d.EquipmentId == id && d.Date >= from && d.Date <= to)
                .Select(d => d.Date)
                .ToListAsync();

            var bookedDates = accepted.SelectMany(b => EachDay(b.StartDate, b.EndDate)).Concat(blocked).Distinct().OrderBy(d => d).ToList();
            var reviewsList = await _reviews.GetReviewsForEquipmentAsync(id);

            return new EquipmentDetailViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Category = e.Category,
                Description = e.Description,
                DailyRate = $"{ListingFormat.Taka(e.DailyRate)} / Day",
                DailyRateAmount = e.DailyRate,
                HourlyRate = e.HourlyRate.HasValue ? $"{ListingFormat.Taka(e.HourlyRate.Value)} / Hour" : string.Empty,
                Location = e.Location,
                Status = e.IsAvailable ? "Available" : "Unavailable",
                OwnerName = e.Owner?.FullName ?? string.Empty,
                OwnerPhone = e.Owner?.PhoneNumber ?? string.Empty,
                OwnerMemberSince = ListingFormat.MemberSince(e.Owner?.CreatedAt ?? e.CreatedAt),
                OwnerRating = e.AverageRating,
                TotalReviews = e.ReviewCount,
                AverageRating = e.AverageRating,
                ReviewCount = e.ReviewCount,
                ImageUrls = ListingFormat.Split(e.ImageUrls),
                BookedDates = bookedDates,
                Reviews = reviewsList
            };
        }

        public async Task<string?> RequestRentalAsync(string farmerId, int equipmentId, DateTime? start, DateTime? end, string? note)
        {
            if (start is null || end is null) return "Please choose a start and end date.";
            var s = start.Value.Date;
            var t = end.Value.Date;
            if (s < DateTime.Today) return "Start date cannot be in the past.";
            if (t < s) return "End date must be on or after the start date.";

            var e = await _equipment.Query().FirstOrDefaultAsync(x => x.Id == equipmentId);
            if (e is null) return "This equipment listing no longer exists.";
            if (!e.IsAvailable) return "This equipment is currently unavailable for rent.";
            if (e.OwnerId == farmerId) return "You cannot rent your own equipment.";

            var clash = await FindConflictAsync(equipmentId, s, t);
            if (clash is not null) return clash;

            await _bookings.AddAsync(new EquipmentBooking
            {
                EquipmentId = equipmentId,
                FarmerId = farmerId,
                StartDate = s,
                EndDate = t,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                Status = BookingStatus.Pending,
                RequestedOn = DateTime.Now
            });
            await _bookings.SaveChangesAsync();
            return null;
        }

        // ---------------------------------------------------------------- Owner

        public async Task<EquipmentOwnerDashboardViewModel> GetOwnerDashboardAsync(string ownerId)
        {
            var today = DateTime.Today;
            var listings = await _equipment.Query()
                .Where(e => e.OwnerId == ownerId)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new
                {
                    e.Id,
                    e.Name,
                    e.Category,
                    e.DailyRate,
                    e.ImageUrls,
                    e.IsAvailable,
                    RentedToday = e.Bookings.Any(b => b.Status == BookingStatus.Accepted && b.StartDate <= today && today <= b.EndDate)
                })
                .ToListAsync();

            var requests = await OwnerBookingsQuery(ownerId).Where(b => b.Status == BookingStatus.Pending).ToListAsync();

            return new EquipmentOwnerDashboardViewModel
            {
                TotalListings = listings.Count,
                ActiveRentals = listings.Count(l => l.RentedToday),
                Listings = listings.Select(l => new OwnerListingItem
                {
                    Id = l.Id,
                    Name = l.Name,
                    Category = l.Category,
                    DailyRate = $"{ListingFormat.Taka(l.DailyRate)} / Day",
                    ImageUrl = ListingFormat.Split(l.ImageUrls).FirstOrDefault() ?? string.Empty,
                    Status = l.RentedToday ? "Rented" : l.IsAvailable ? "Available" : "Unavailable"
                }).ToList(),
                PendingRequestItems = requests.Select(ToRequestItem).OrderByDescending(r => r.RequestedOn).ToList()
            };
        }

        public async Task<RentalRequestsViewModel> GetOwnerRequestsAsync(string ownerId)
        {
            var bookings = await OwnerBookingsQuery(ownerId).ToListAsync();
            var items = bookings.Select(ToRequestItem).ToList();
            var accepted = bookings.Where(b => b.Status == BookingStatus.Accepted).ToList();

            foreach (var pending in items.Where(i => i.Status == BookingStatus.Pending))
            {
                var source = bookings.First(b => b.Id == pending.Id);
                var clash = accepted.FirstOrDefault(a => a.EquipmentId == source.EquipmentId
                    && BookingWorkflow.Overlaps(a.StartDate, a.EndDate, source.StartDate, source.EndDate));
                if (clash is null) continue;
                pending.HasConflict = true;
                pending.ConflictHint = $"Dates overlap with an accepted rental ({ListingFormat.DateRange(clash.StartDate, clash.EndDate)})";
            }

            return new RentalRequestsViewModel { Requests = items.OrderByDescending(r => r.RequestedOn).ToList() };
        }

        public Task<int> CountPendingAsync(string ownerId) =>
            _bookings.Query().CountAsync(b => b.Equipment!.OwnerId == ownerId && b.Status == BookingStatus.Pending);

        public async Task<DecisionResult> RespondAsync(string ownerId, int bookingId, string decision, string? reason)
        {
            var booking = await _bookings.QueryTracked().Include(b => b.Equipment)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.Equipment!.OwnerId == ownerId);
            if (booking is null) return DecisionResult.Fail("This request could not be found.");

            var next = BookingWorkflow.Next(booking.Status, decision);
            if (next is null) return DecisionResult.Fail($"A {booking.Status.ToLowerInvariant()} request cannot be {decision.ToLowerInvariant()}ed.");

            var autoRejected = new List<int>();
            if (next == BookingStatus.Accepted)
            {
                var clash = await FindConflictAsync(booking.EquipmentId, booking.StartDate, booking.EndDate, excludeBookingId: booking.Id);
                if (clash is not null) return DecisionResult.Fail(clash);

                // First accepted wins: every other pending request that overlaps these dates is declined with a reason.
                var losers = await _bookings.QueryTracked()
                    .Where(b => b.EquipmentId == booking.EquipmentId && b.Id != booking.Id && b.Status == BookingStatus.Pending
                        && b.StartDate <= booking.EndDate && booking.StartDate <= b.EndDate)
                    .ToListAsync();
                foreach (var loser in losers)
                {
                    loser.Status = BookingStatus.Rejected;
                    loser.RejectReason = BookingWorkflow.AutoRejectReason;
                    loser.UpdatedOn = DateTime.Now;
                    autoRejected.Add(loser.Id);
                }
            }

            booking.Status = next;
            booking.RejectReason = next == BookingStatus.Rejected && !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : null;
            booking.UpdatedOn = DateTime.Now;
            await _bookings.SaveChangesAsync();
            return DecisionResult.Ok(autoRejected);
        }

        public async Task<EquipmentListingViewModel?> GetListingAsync(string ownerId, int id)
        {
            var e = await _equipment.Query().FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId);
            if (e is null) return null;

            return new EquipmentListingViewModel
            {
                Id = e.Id,
                Name = e.Name,
                Category = e.Category,
                Description = e.Description,
                Location = e.Location,
                DailyRate = e.DailyRate,
                HourlyRate = e.HourlyRate,
                IsAvailable = e.IsAvailable,
                ExistingImageUrls = ListingFormat.Split(e.ImageUrls)
            };
        }

        public async Task<bool> SaveListingAsync(string ownerId, EquipmentListingViewModel model)
        {
            Equipment entity;
            if (model.IsEditMode)
            {
                var existing = await _equipment.QueryTracked().FirstOrDefaultAsync(x => x.Id == model.Id && x.OwnerId == ownerId);
                if (existing is null) return false;
                entity = existing;
            }
            else
            {
                entity = new Equipment { OwnerId = ownerId, CreatedAt = DateTime.UtcNow };
                await _equipment.AddAsync(entity);
            }

            var images = model.ExistingImageUrls.Concat(await _files.SaveImagesAsync(model.ImageFiles, UploadFolder));

            entity.Name = model.Name.Trim();
            entity.Category = model.Category.Trim();
            entity.Description = model.Description.Trim();
            entity.Location = model.Location.Trim();
            entity.DailyRate = model.DailyRate;
            entity.HourlyRate = model.HourlyRate is > 0 ? model.HourlyRate : null;
            entity.IsAvailable = model.IsAvailable;
            entity.ImageUrls = ListingFormat.Join(images);

            await _equipment.SaveChangesAsync();
            model.Id = entity.Id;
            return true;
        }

        public async Task<ManageAvailabilityViewModel?> GetAvailabilityAsync(string ownerId, int equipmentId, DateTime? month)
        {
            var e = await _equipment.Query().FirstOrDefaultAsync(x => x.Id == equipmentId && x.OwnerId == ownerId);
            if (e is null) return null;

            var first = new DateTime((month ?? DateTime.Today).Year, (month ?? DateTime.Today).Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            var accepted = await _bookings.Query()
                .Where(b => b.EquipmentId == equipmentId && b.Status == BookingStatus.Accepted && b.EndDate >= first && b.StartDate <= last)
                .Select(b => new { b.StartDate, b.EndDate })
                .ToListAsync();
            var blocked = await _blockedDates.Query()
                .Where(d => d.EquipmentId == equipmentId && d.Date >= first && d.Date <= last)
                .Select(d => d.Date)
                .ToListAsync();

            return new ManageAvailabilityViewModel
            {
                ListingId = e.Id,
                ListingName = e.Name,
                Category = e.Category,
                RateText = $"{ListingFormat.Taka(e.DailyRate)} / Day",
                Location = e.Location,
                ThumbnailUrl = ListingFormat.Split(e.ImageUrls).FirstOrDefault() ?? string.Empty,
                Month = first,
                MonthName = first.ToString("MMMM yyyy"),
                FarmerBookedDates = accepted.SelectMany(b => EachDay(b.StartDate, b.EndDate)).Where(d => d >= first && d <= last).Distinct().ToList(),
                OwnerBlockedDates = blocked
            };
        }

        public async Task<bool> SaveAvailabilityAsync(string ownerId, int equipmentId, DateTime month, IEnumerable<DateTime> blockedDates)
        {
            if (!await _equipment.Query().AnyAsync(x => x.Id == equipmentId && x.OwnerId == ownerId)) return false;

            var first = new DateTime(month.Year, month.Month, 1);
            var last = first.AddMonths(1).AddDays(-1);

            var current = await _blockedDates.QueryTracked()
                .Where(d => d.EquipmentId == equipmentId && d.Date >= first && d.Date <= last)
                .ToListAsync();
            _blockedDates.RemoveRange(current);

            var farmerBooked = (await _bookings.Query()
                    .Where(b => b.EquipmentId == equipmentId && b.Status == BookingStatus.Accepted && b.EndDate >= first && b.StartDate <= last)
                    .Select(b => new { b.StartDate, b.EndDate })
                    .ToListAsync())
                .SelectMany(b => EachDay(b.StartDate, b.EndDate))
                .ToHashSet();

            foreach (var date in blockedDates.Select(d => d.Date).Distinct().Where(d => d >= first && d <= last && !farmerBooked.Contains(d)))
                await _blockedDates.AddAsync(new EquipmentBlockedDate { EquipmentId = equipmentId, Date = date });

            await _blockedDates.SaveChangesAsync();
            return true;
        }

        // ---------------------------------------------------------------- Helpers

        /// <summary>
        /// The single source of truth for equipment double-booking: an accepted (or ongoing) rental or an owner-blocked
        /// date inside [start, end] makes the range unavailable. Returns a user-facing message, or null when free.
        /// </summary>
        private async Task<string?> FindConflictAsync(int equipmentId, DateTime start, DateTime end, int? excludeBookingId = null)
        {
            var accepted = await _bookings.Query()
                .Where(b => b.EquipmentId == equipmentId && b.Id != excludeBookingId && b.Status == BookingStatus.Accepted
                    && b.StartDate <= end && start <= b.EndDate)
                .OrderBy(b => b.StartDate)
                .Select(b => new { b.StartDate, b.EndDate })
                .FirstOrDefaultAsync();
            if (accepted is not null)
                return $"These dates overlap an accepted rental ({ListingFormat.DateRange(accepted.StartDate, accepted.EndDate)}). Please choose different dates.";

            var blocked = await _blockedDates.Query()
                .Where(d => d.EquipmentId == equipmentId && d.Date >= start && d.Date <= end)
                .OrderBy(d => d.Date)
                .Select(d => (DateTime?)d.Date)
                .FirstOrDefaultAsync();
            return blocked is null ? null : $"The owner has marked {blocked:dd MMM yyyy} as unavailable. Please choose different dates.";
        }

        private IQueryable<EquipmentBooking> OwnerBookingsQuery(string ownerId) =>
            _bookings.Query()
                .Include(b => b.Equipment)
                .Include(b => b.Farmer)
                .Where(b => b.Equipment!.OwnerId == ownerId);

        private static RentalRequestItem ToRequestItem(EquipmentBooking b) => new()
        {
            Id = b.Id,
            FarmerName = string.IsNullOrWhiteSpace(b.Farmer?.FullName) ? "Farmer" : b.Farmer!.FullName,
            EquipmentName = b.Equipment?.Name ?? string.Empty,
            EquipmentCategory = b.Equipment?.Category ?? string.Empty,
            DailyRate = $"{ListingFormat.Taka(b.Equipment?.DailyRate ?? 0)} / Day",
            Location = b.Equipment?.Location ?? string.Empty,
            DateRange = ListingFormat.DateRange(b.StartDate, b.EndDate),
            StartDate = b.StartDate,
            EndDate = b.EndDate,
            Note = b.Note,
            Status = b.Status,
            RejectReason = b.RejectReason,
            RequestedOn = b.RequestedOn
        };

        private static IEnumerable<DateTime> EachDay(DateTime start, DateTime end)
        {
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1)) yield return d;
        }
    }
}
