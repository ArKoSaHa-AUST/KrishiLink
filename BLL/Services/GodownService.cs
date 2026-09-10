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
        Task<bool> RespondAsync(string ownerId, int bookingId, string decision, string? reason);
        Task<GodownListingViewModel?> GetListingAsync(string ownerId, int id);
        Task<bool> SaveListingAsync(string ownerId, GodownListingViewModel model);
    }

    public class GodownService : IGodownService
    {
        private const string UploadFolder = "godowns";
        private readonly IRepository<Godown> _godowns;
        private readonly IRepository<GodownBooking> _bookings;
        private readonly IFileStorageService _files;

        public GodownService(IRepository<Godown> godowns, IRepository<GodownBooking> bookings, IFileStorageService files)
        {
            _godowns = godowns;
            _bookings = bookings;
            _files = files;
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

            return new GodownDetailViewModel
            {
                Id = g.Id,
                Name = g.Name,
                StorageType = g.StorageType,
                Location = g.Location,
                Description = g.Description,
                TotalCapacityTons = g.CapacityInTons,
                AvailableCapacityTons = available,
                PricePerTonPerMonth = $"{ListingFormat.Taka(g.PricePerTonPerMonth)} / Ton / Month",
                DailyRatePerTon = $"{ListingFormat.Taka(Math.Round(g.PricePerTonPerMonth / 30m, 2))} / Ton / Day",
                Status = !g.IsActive ? "Inactive" : available > 0 ? "Available" : "Fully Booked",
                OwnerName = g.Owner?.FullName ?? string.Empty,
                OwnerPhone = g.Owner?.PhoneNumber ?? string.Empty,
                OwnerMemberSince = ListingFormat.MemberSince(g.Owner?.CreatedAt ?? g.CreatedAt),
                ImageUrls = ListingFormat.Split(g.ImageUrls),
                Facilities = ListingFormat.Split(g.Facilities),
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddMonths(1),
                RequestedCapacityTons = Math.Min(10, available)
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

            var free = g.CapacityInTons - await OccupiedTonsAsync(g.Id, s, t);
            if (r.RequestedCapacityTons > free)
                return $"Only {free:N0} tons are free for the selected period. Please reduce the quantity or change the dates.";

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

        public async Task<bool> RespondAsync(string ownerId, int bookingId, string decision, string? reason)
        {
            var booking = await _bookings.QueryTracked().Include(b => b.Godown)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.Godown!.OwnerId == ownerId);
            if (booking is null) return false;

            var next = BookingWorkflow.Next(booking.Status, decision);
            if (next is null) return false;

            booking.Status = next;
            booking.RejectReason = next == BookingStatus.Rejected && !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : null;
            booking.UpdatedOn = DateTime.Now;
            await _bookings.SaveChangesAsync();
            return true;
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

        // ---------------------------------------------------------------- Helpers

        private static double ToTons(double quantity, string unit) => unit switch
        {
            "Maunds" => quantity * 0.04,
            "Bags" => quantity * 0.05,
            "Quintals" => quantity * 0.1,
            _ => quantity
        };

        private async Task<double> OccupiedTonsAsync(int godownId, DateTime from, DateTime to) =>
            await _bookings.Query()
                .Where(b => b.GodownId == godownId && b.Status == BookingStatus.Accepted && b.StartDate <= to && from <= b.EndDate)
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
