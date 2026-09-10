using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    /// <summary>Farmer-side view of their own equipment rentals and godown storage bookings.</summary>
    public interface IBookingService
    {
        Task<BookingHistoryViewModel> GetHistoryAsync(string farmerId, string tab, string status, DateTime? from, DateTime? to, string? search);
        Task<FarmerDashboardViewModel> GetDashboardAsync(string farmerId);
    }

    public class BookingService : IBookingService
    {
        private readonly IRepository<EquipmentBooking> _rentals;
        private readonly IRepository<GodownBooking> _storage;

        public BookingService(IRepository<EquipmentBooking> rentals, IRepository<GodownBooking> storage)
        {
            _rentals = rentals;
            _storage = storage;
        }

        public async Task<BookingHistoryViewModel> GetHistoryAsync(string farmerId, string tab, string status, DateTime? from, DateTime? to, string? search)
        {
            var all = await LoadAllAsync(farmerId);
            tab = string.IsNullOrWhiteSpace(tab) ? "all" : tab.ToLowerInvariant();
            status = string.IsNullOrWhiteSpace(status) ? "all" : status.ToLowerInvariant();

            IEnumerable<BookingHistoryItemViewModel> query = all;
            if (tab is "equipment" or "godown")
                query = query.Where(b => b.BookingType.Equals(tab, StringComparison.OrdinalIgnoreCase));
            if (status != "all")
                query = query.Where(b => b.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b => b.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || b.BookingCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || b.Location.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || b.OwnerName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            if (from.HasValue) query = query.Where(b => b.StartDate >= from.Value.Date);
            if (to.HasValue) query = query.Where(b => b.EndDate <= to.Value.Date);

            return new BookingHistoryViewModel
            {
                ActiveTab = tab,
                StatusFilter = status,
                DateFrom = from,
                DateTo = to,
                SearchTerm = search,
                Bookings = query.OrderByDescending(b => b.RequestedAt).ToList(),
                TotalAllCount = all.Count,
                EquipmentCount = all.Count(b => b.BookingType == "Equipment"),
                GodownCount = all.Count(b => b.BookingType == "Godown"),
                PendingCount = all.Count(b => b.Status == BookingStatus.Pending),
                AcceptedCount = all.Count(b => b.Status == BookingStatus.Accepted),
                CompletedCount = all.Count(b => b.Status == BookingStatus.Completed),
                RejectedCount = all.Count(b => b.Status == BookingStatus.Rejected)
            };
        }

        public async Task<FarmerDashboardViewModel> GetDashboardAsync(string farmerId)
        {
            var all = await LoadAllAsync(farmerId);

            var activity = new List<(DateTime At, ActivityFeedItem Item)>();
            foreach (var b in all)
            {
                var noun = b.BookingType == "Equipment" ? "Equipment request" : "Godown booking request";
                activity.Add((b.RequestedAt, Feed($"{noun} sent for {b.ItemName}.", b.BookingType == "Equipment" ? "bi-tools" : "bi-building", "text-warning", b.RequestedAt)));

                if (b.UpdatedAt is null || b.Status == BookingStatus.Pending) continue;
                var (text, icon, color) = b.Status switch
                {
                    BookingStatus.Accepted => ($"{b.BookingType} request for {b.ItemName} was accepted by the owner.", "bi-check-circle-fill", "text-success"),
                    BookingStatus.Rejected => ($"{b.BookingType} request for {b.ItemName} was rejected by the owner.", "bi-x-circle-fill", "text-danger"),
                    BookingStatus.Completed => ($"{b.ItemName} booking completed.", "bi-flag-fill", "text-secondary"),
                    _ => ($"{b.ItemName} booking was {b.Status.ToLowerInvariant()}.", "bi-info-circle-fill", "text-secondary")
                };
                activity.Add((b.UpdatedAt.Value, Feed(text, icon, color, b.UpdatedAt.Value)));
            }

            return new FarmerDashboardViewModel
            {
                ActiveBookings = all
                    .Where(b => b.Status is BookingStatus.Pending or BookingStatus.Accepted)
                    .OrderBy(b => b.StartDate)
                    .Take(6)
                    .Select(b => new BookingSummaryItem
                    {
                        Id = b.Id,
                        ItemName = b.ItemName,
                        BookingType = b.BookingType,
                        Location = b.Location,
                        DateRange = b.DateRangeDisplay,
                        Status = b.Status,
                        DetailUrl = "/Bookings"
                    }).ToList(),
                RecentActivity = activity.OrderByDescending(a => a.At).Take(6).Select(a => a.Item).ToList()
            };
        }

        // ---------------------------------------------------------------- Mapping

        private async Task<List<BookingHistoryItemViewModel>> LoadAllAsync(string farmerId)
        {
            var rentals = await _rentals.Query()
                .Include(b => b.Equipment!).ThenInclude(e => e.Owner)
                .Where(b => b.FarmerId == farmerId)
                .ToListAsync();
            var storage = await _storage.Query()
                .Include(b => b.Godown!).ThenInclude(g => g.Owner)
                .Where(b => b.FarmerId == farmerId)
                .ToListAsync();

            return rentals.Select(ToItem).Concat(storage.Select(ToItem)).ToList();
        }

        private static BookingHistoryItemViewModel ToItem(EquipmentBooking b)
        {
            var e = b.Equipment!;
            var days = ListingFormat.InclusiveDays(b.StartDate, b.EndDate);
            var item = new BookingHistoryItemViewModel
            {
                Id = b.Id,
                BookingCode = $"KL-EQ-{b.RequestedOn.Year}-{b.Id:D3}",
                ItemName = e.Name,
                BookingType = "Equipment",
                Category = e.Category,
                ImageUrl = ListingFormat.Split(e.ImageUrls).FirstOrDefault() ?? string.Empty,
                Location = e.Location,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                TotalCost = days * e.DailyRate,
                RateDescription = $"{ListingFormat.Taka(e.DailyRate)} / day × {days} {(days == 1 ? "day" : "days")}",
                QuantityDisplay = $"1 {e.Category}",
                ListingDetailUrl = $"/Equipment/Details/{e.Id}"
            };
            return Finish(item, b.Status, b.Note, b.RejectReason, b.RequestedOn, b.UpdatedOn, e.Owner, "Rental Requested", "Active in Field", "Equipment in use", "Completed & Handover");
        }

        private static BookingHistoryItemViewModel ToItem(GodownBooking b)
        {
            var g = b.Godown!;
            var months = ListingFormat.Months(b.StartDate, b.EndDate);
            var item = new BookingHistoryItemViewModel
            {
                Id = b.Id,
                BookingCode = $"KL-GD-{b.RequestedOn.Year}-{b.Id:D3}",
                ItemName = g.Name,
                BookingType = "Godown",
                Category = g.StorageType,
                ImageUrl = ListingFormat.Split(g.ImageUrls).FirstOrDefault() ?? string.Empty,
                Location = g.Location,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                TotalCost = decimal.Round((decimal)b.StorageTons * g.PricePerTonPerMonth * (decimal)months, 0),
                RateDescription = $"{ListingFormat.Taka(g.PricePerTonPerMonth)} / ton / mo × {b.StorageTons:N0} Tons × {months:0.#} Months",
                QuantityDisplay = $"{b.StorageTons:N0} Tons Capacity",
                ListingDetailUrl = $"/Godown/Details/{g.Id}"
            };
            return Finish(item, b.Status, b.Note, b.RejectReason, b.RequestedOn, b.UpdatedOn, g.Owner, "Booking Requested", "Produce Stored", "Goods in storage", "Storage Period Ended");
        }

        private static BookingHistoryItemViewModel Finish(BookingHistoryItemViewModel item, string status, string? note, string? rejectReason,
            DateTime requestedOn, DateTime? updatedOn, ApplicationUser? owner, string requestedTitle, string activeTitle, string activeDesc, string completedTitle)
        {
            item.Status = status;
            item.RequestedAt = requestedOn;
            item.UpdatedAt = updatedOn;
            item.OwnerName = owner?.FullName ?? string.Empty;
            item.OwnerPhone = owner?.PhoneNumber ?? string.Empty;
            item.FarmerNotes = note ?? string.Empty;
            item.OwnerRemarks = rejectReason;
            item.PaymentStatus = status switch
            {
                BookingStatus.Completed => "Paid on Service",
                BookingStatus.Accepted => "Pending on Delivery",
                BookingStatus.Pending => "Unpaid (Awaiting Confirmation)",
                _ => "Not Applicable"
            };

            var today = DateTime.Today;
            var decided = updatedOn?.ToString("dd MMM yyyy, hh:mm tt");
            var range = ListingFormat.DateRange(item.StartDate, item.EndDate);
            item.Timeline = new List<BookingTimelineStep>
            {
                new() { Title = requestedTitle, Description = "Request submitted", DateDisplay = requestedOn.ToString("dd MMM yyyy, hh:mm tt"), IsCompleted = true, State = "done" }
            };

            switch (status)
            {
                case BookingStatus.Pending:
                    item.Timeline.Add(new() { Title = "Owner Review", Description = $"{item.OwnerName} is reviewing your request", DateDisplay = "In Progress", IsCurrent = true, State = "active" });
                    item.Timeline.Add(new() { Title = activeTitle, Description = activeDesc, DateDisplay = range, State = "pending" });
                    item.Timeline.Add(new() { Title = completedTitle, Description = "Final handover and payment", DateDisplay = $"Expected {item.EndDate:dd MMM yyyy}", State = "pending" });
                    break;
                case BookingStatus.Rejected:
                case BookingStatus.Cancelled:
                    item.Timeline.Add(new() { Title = status, Description = rejectReason ?? $"Request {status.ToLowerInvariant()}", DateDisplay = decided, IsCompleted = true, IsCurrent = true, State = "rejected" });
                    break;
                default:
                    var inField = status == BookingStatus.Accepted && item.StartDate <= today;
                    var completed = status == BookingStatus.Completed;
                    item.Timeline.Add(new() { Title = "Owner Accepted", Description = $"Accepted by {item.OwnerName}", DateDisplay = decided, IsCompleted = true, State = "done" });
                    item.Timeline.Add(new() { Title = activeTitle, Description = activeDesc, DateDisplay = range, IsCompleted = completed, IsCurrent = inField && !completed, State = completed ? "done" : inField ? "active" : "pending" });
                    item.Timeline.Add(new() { Title = completedTitle, Description = "Final handover and payment", DateDisplay = completed ? decided : $"Expected {item.EndDate:dd MMM yyyy}", IsCompleted = completed, IsCurrent = completed, State = completed ? "done" : "pending" });
                    break;
            }

            return item;
        }

        private static ActivityFeedItem Feed(string text, string icon, string color, DateTime at) =>
            new() { Description = text, IconClass = icon, IconColor = color, TimeAgo = TimeAgoFormatter.Format(at) };
    }
}
