using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    public interface IHarvestPlanService
    {
        Task<HarvestPlanIndexViewModel> GetPlansAsync(string farmerId);
        Task<HarvestPlanDetailsViewModel?> GetDetailsAsync(string farmerId, int planId);
        Task<(string? Error, int? PlanId)> CreatePlanAsync(string farmerId, string name, string? crop = null, string? note = null);
        Task<(string? Error, int? PlanId, int? ItemId)> AddItemAsync(string farmerId, HarvestPlanItemInput input);
        Task<string?> UpdateItemAsync(string farmerId, int itemId, DateTime start, DateTime end, int? units, double? tons, string? note);
        Task<string?> RemoveItemAsync(string farmerId, int itemId);
        Task<string?> UpdatePlanAsync(string farmerId, int planId, string name, string? crop = null, string? note = null);
        Task<string?> DeleteAsync(string farmerId, int planId);
        Task<HarvestPlanSubmitResult> SubmitAsync(string farmerId, int planId);
        Task<(string? Error, int? NewPlanId)> CloneAsync(string farmerId, int planId, int shiftDays = 365);
        Task<int> CountDraftItemsAsync(string farmerId);
        Task<List<HarvestPlanOptionViewModel>> GetDraftPlansForSelectionAsync(string farmerId);
    }

    public class HarvestPlanService : IHarvestPlanService
    {
        private const int MaxDraftPlans = 5;
        private const int MaxItemsPerPlan = 10;

        private readonly IRepository<HarvestPlan> _harvestPlans;
        private readonly IRepository<HarvestPlanItem> _items;
        private readonly IRepository<Equipment> _equipment;
        private readonly IRepository<Godown> _godowns;
        private readonly IRepository<EquipmentBooking> _equipmentBookings;
        private readonly IRepository<GodownBooking> _godownBookings;
        private readonly IEquipmentService _equipmentService;
        private readonly IGodownService _godownService;
        private readonly INotificationService _notifications;

        public HarvestPlanService(
            IRepository<HarvestPlan> harvestPlans,
            IRepository<HarvestPlanItem> items,
            IRepository<Equipment> equipment,
            IRepository<Godown> godowns,
            IRepository<EquipmentBooking> equipmentBookings,
            IRepository<GodownBooking> godownBookings,
            IEquipmentService equipmentService,
            IGodownService godownService,
            INotificationService notifications)
        {
            _harvestPlans = harvestPlans;
            _items = items;
            _equipment = equipment;
            _godowns = godowns;
            _equipmentBookings = equipmentBookings;
            _godownBookings = godownBookings;
            _equipmentService = equipmentService;
            _godownService = godownService;
            _notifications = notifications;
        }

        public async Task<HarvestPlanIndexViewModel> GetPlansAsync(string farmerId)
        {
            var plans = await _harvestPlans.Query()
                .Include(p => p.Items)
                .Where(p => p.FarmerId == farmerId)
                .OrderBy(p => p.Status == HarvestPlanStatus.Draft ? 0 : p.Status == HarvestPlanStatus.Submitted ? 1 : 2)
                .ThenByDescending(p => p.CreatedAt)
                .ToListAsync();

            var allEqIds = plans.SelectMany(p => p.Items.Where(i => i.ItemType == HarvestPlanItemType.Equipment).Select(i => i.ListingId)).Distinct().ToList();
            var allGdIds = plans.SelectMany(p => p.Items.Where(i => i.ItemType == HarvestPlanItemType.Godown).Select(i => i.ListingId)).Distinct().ToList();

            var eqRates = await _equipment.Query()
                .Where(e => allEqIds.Contains(e.Id))
                .Select(e => new { e.Id, e.DailyRate })
                .ToDictionaryAsync(e => e.Id, e => e.DailyRate);

            var gdRates = await _godowns.Query()
                .Where(g => allGdIds.Contains(g.Id))
                .Select(g => new { g.Id, g.PricePerTonPerMonth })
                .ToDictionaryAsync(g => g.Id, g => g.PricePerTonPerMonth);

            var items = new List<HarvestPlanListViewModel>();

            foreach (var p in plans)
            {
                decimal totalGross = 0m;
                foreach (var i in p.Items)
                {
                    if (i.ItemType == HarvestPlanItemType.Equipment && eqRates.TryGetValue(i.ListingId, out var dailyRate))
                    {
                        totalGross += BookingPricing.EquipmentGross(i.StartDate, i.EndDate, dailyRate, i.Units);
                    }
                    else if (i.ItemType == HarvestPlanItemType.Godown && gdRates.TryGetValue(i.ListingId, out var pricePerTon))
                    {
                        totalGross += BookingPricing.GodownGross(i.StartDate, i.EndDate, i.Tons, pricePerTon);
                    }
                }

                string dateRangeSummary = p.Items.Count > 0
                    ? ListingFormat.DateRange(p.Items.Min(i => i.StartDate), p.Items.Max(i => i.EndDate))
                    : "No items";

                items.Add(new HarvestPlanListViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Crop = p.Crop,
                    Note = p.Note,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    SubmittedOn = p.SubmittedOn,
                    ItemCount = p.Items.Count,
                    TotalEstimatedGross = totalGross,
                    DateRangeSummary = dateRangeSummary
                });
            }

            return new HarvestPlanIndexViewModel
            {
                Plans = items
            };
        }

        public async Task<(string? Error, int? PlanId)> CreatePlanAsync(string farmerId, string name, string? crop = null, string? note = null)
        {
            var draftCount = await _harvestPlans.Query()
                .CountAsync(p => p.FarmerId == farmerId && p.Status == HarvestPlanStatus.Draft);

            if (draftCount >= MaxDraftPlans)
            {
                return ($"You can have at most {MaxDraftPlans} draft harvest plans at a time.", null);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Harvest Plan {DateTime.Now:MMM yyyy}";
            }

            var plan = new HarvestPlan
            {
                FarmerId = farmerId,
                Name = name.Trim(),
                Crop = string.IsNullOrWhiteSpace(crop) ? null : crop.Trim(),
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                Status = HarvestPlanStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            await _harvestPlans.AddAsync(plan);
            await _harvestPlans.SaveChangesAsync();

            return (null, plan.Id);
        }

        public async Task<HarvestPlanDetailsViewModel?> GetDetailsAsync(string farmerId, int planId)
        {
            var plan = await _harvestPlans.QueryTracked()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == planId && p.FarmerId == farmerId);

            if (plan == null) return null;

            // Load associated equipment, godowns, and bookings in batches
            var eqIds = plan.Items.Where(i => i.ItemType == HarvestPlanItemType.Equipment).Select(i => i.ListingId).Distinct().ToList();
            var gdIds = plan.Items.Where(i => i.ItemType == HarvestPlanItemType.Godown).Select(i => i.ListingId).Distinct().ToList();

            var equipments = await _equipment.Query()
                .Include(e => e.Owner)
                .Where(e => eqIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id);

            var godowns = await _godowns.Query()
                .Include(g => g.Owner)
                .Where(g => gdIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id);

            var eqBookingIds = plan.Items.Where(i => i.ItemType == HarvestPlanItemType.Equipment && i.BookingId.HasValue).Select(i => i.BookingId!.Value).Distinct().ToList();
            var gdBookingIds = plan.Items.Where(i => i.ItemType == HarvestPlanItemType.Godown && i.BookingId.HasValue).Select(i => i.BookingId!.Value).Distinct().ToList();

            var eqBookings = await _equipmentBookings.Query()
                .Include(b => b.Payment)
                .Where(b => eqBookingIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            var gdBookings = await _godownBookings.Query()
                .Include(b => b.Payment)
                .Where(b => gdBookingIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            // Auto-close if Submitted and all bookings are terminal
            if (plan.Status == HarvestPlanStatus.Submitted && plan.Items.Count > 0)
            {
                var allTerminal = plan.Items.All(i =>
                {
                    if (i.ItemType == HarvestPlanItemType.Equipment)
                    {
                        return i.BookingId.HasValue && eqBookings.TryGetValue(i.BookingId.Value, out var b)
                            && (b.Status is BookingStatus.Completed or BookingStatus.Rejected or BookingStatus.Cancelled);
                    }
                    else
                    {
                        return i.BookingId.HasValue && gdBookings.TryGetValue(i.BookingId.Value, out var b)
                            && (b.Status is BookingStatus.Completed or BookingStatus.Rejected or BookingStatus.Cancelled);
                    }
                });

                if (allTerminal)
                {
                    plan.Status = HarvestPlanStatus.Closed;
                    await _harvestPlans.SaveChangesAsync();
                }
            }

            var itemVms = new List<HarvestPlanItemDetailViewModel>();
            decimal liveTotalGross = 0m;
            bool hasConflicts = false;

            int pendingCount = 0;
            int acceptedCount = 0;
            int paidCount = 0;
            int completedCount = 0;
            int rejectedCount = 0;
            int cancelledCount = 0;

            foreach (var item in plan.Items.OrderBy(i => i.StartDate))
            {
                bool isAvailable = true;
                string? conflictReason = null;
                decimal itemGross = 0m;
                string title = string.Empty;
                string category = string.Empty;
                string imageUrl = string.Empty;
                string location = string.Empty;
                string ownerId = string.Empty;
                string ownerName = string.Empty;
                string ownerPhone = string.Empty;
                string listingUrl = string.Empty;
                int maxUnits = 1;
                int minRentalDays = 1;
                double capacityTons = 0;
                string quantityDisplay = string.Empty;

                if (item.ItemType == HarvestPlanItemType.Equipment)
                {
                    if (equipments.TryGetValue(item.ListingId, out var eq))
                    {
                        title = eq.Name;
                        category = eq.Category;
                        imageUrl = ListingFormat.Split(eq.ImageUrls).FirstOrDefault() ?? string.Empty;
                        location = eq.Location;
                        ownerId = eq.OwnerId;
                        ownerName = eq.Owner?.FullName ?? "Equipment Owner";
                        ownerPhone = eq.Owner?.PhoneNumber ?? string.Empty;
                        listingUrl = $"/Equipment/Details/{eq.Id}";
                        maxUnits = eq.Quantity;
                        minRentalDays = eq.MinRentalDays;
                        quantityDisplay = item.Units > 1 ? $"{item.Units} units" : "1 unit";

                        if (!eq.IsAvailable)
                        {
                            isAvailable = false;
                            conflictReason = "Equipment is currently marked unavailable.";
                        }
                        else
                        {
                            conflictReason = await _equipmentService.CheckAvailabilityAsync(
                                eq.Id,
                                item.StartDate,
                                item.EndDate,
                                item.Units,
                                excludeBookingId: item.BookingId);
                            isAvailable = conflictReason == null;
                        }

                        var (quotedGross, _) = await _equipmentService.QuoteGrossAsync(eq.Id, item.StartDate, item.EndDate, item.Units);
                        itemGross = quotedGross;
                    }
                    else
                    {
                        title = $"Equipment #{item.ListingId}";
                        isAvailable = false;
                        conflictReason = "Equipment listing not found.";
                    }
                }
                else
                {
                    if (godowns.TryGetValue(item.ListingId, out var gd))
                    {
                        title = gd.Name;
                        category = gd.StorageType;
                        imageUrl = ListingFormat.Split(gd.ImageUrls).FirstOrDefault() ?? string.Empty;
                        location = gd.Location;
                        ownerId = gd.OwnerId;
                        ownerName = gd.Owner?.FullName ?? "Godown Owner";
                        ownerPhone = gd.Owner?.PhoneNumber ?? string.Empty;
                        listingUrl = $"/Godown/Details/{gd.Id}";
                        capacityTons = gd.CapacityInTons;
                        quantityDisplay = $"{item.Tons:N0} Tons";

                        if (!gd.IsActive)
                        {
                            isAvailable = false;
                            conflictReason = "Storage facility is currently not accepting bookings.";
                        }
                        else
                        {
                            conflictReason = await _godownService.CheckAvailabilityAsync(
                                gd.Id,
                                item.Tons,
                                item.StartDate,
                                item.EndDate,
                                excludeBookingId: item.BookingId);
                            isAvailable = conflictReason == null;
                        }

                        itemGross = BookingPricing.GodownGross(item.StartDate, item.EndDate, item.Tons, gd.PricePerTonPerMonth);
                    }
                    else
                    {
                        title = $"Storage #{item.ListingId}";
                        isAvailable = false;
                        conflictReason = "Storage facility not found.";
                    }
                }

                if (!isAvailable) hasConflicts = true;
                liveTotalGross += itemGross;

                string? bookingStatus = null;
                string? bookingCode = null;
                bool canPay = false;
                string? payUrl = null;
                string? passUrl = null;
                decimal? agreedGross = null;

                if (item.BookingId.HasValue)
                {
                    if (item.ItemType == HarvestPlanItemType.Equipment && eqBookings.TryGetValue(item.BookingId.Value, out var eb))
                    {
                        bookingStatus = eb.Status;
                        bookingCode = $"KL-EQ-{eb.RequestedOn.Year}-{eb.Id:D3}";
                        agreedGross = BookingPricing.EquipmentGrossOf(eb, equipments.TryGetValue(eb.EquipmentId, out var e) ? e.DailyRate : 0);
                        canPay = eb.Status == BookingStatus.Accepted;
                        payUrl = $"/Bookings/Pay?type=Equipment&id={eb.Id}";
                        passUrl = $"/Bookings/Pass/Equipment/{eb.Id}";
                    }
                    else if (item.ItemType == HarvestPlanItemType.Godown && gdBookings.TryGetValue(item.BookingId.Value, out var gb))
                    {
                        bookingStatus = gb.Status;
                        bookingCode = $"KL-GD-{gb.RequestedOn.Year}-{gb.Id:D3}";
                        agreedGross = gb.AgreedGross ?? BookingPricing.GodownGross(gb.StartDate, gb.EndDate, gb.StorageTons, godowns.TryGetValue(gb.GodownId, out var g) ? g.PricePerTonPerMonth : 0);
                        canPay = gb.Status == BookingStatus.Accepted;
                        payUrl = $"/Bookings/Pay?type=Godown&id={gb.Id}";
                        passUrl = $"/Bookings/Pass/Godown/{gb.Id}";
                    }

                    if (bookingStatus == BookingStatus.Pending) pendingCount++;
                    else if (bookingStatus == BookingStatus.Accepted) acceptedCount++;
                    else if (bookingStatus == BookingStatus.Paid) paidCount++;
                    else if (bookingStatus == BookingStatus.Completed) completedCount++;
                    else if (bookingStatus == BookingStatus.Rejected) rejectedCount++;
                    else if (bookingStatus == BookingStatus.Cancelled) cancelledCount++;
                }

                var days = ListingFormat.InclusiveDays(item.StartDate, item.EndDate);

                itemVms.Add(new HarvestPlanItemDetailViewModel
                {
                    Id = item.Id,
                    HarvestPlanId = item.HarvestPlanId,
                    ItemType = item.ItemType,
                    ListingId = item.ListingId,
                    Title = title,
                    CategoryOrType = category,
                    ImageUrl = imageUrl,
                    Location = location,
                    OwnerId = ownerId,
                    OwnerName = ownerName,
                    OwnerPhone = ownerPhone,
                    ListingDetailUrl = listingUrl,
                    StartDate = item.StartDate,
                    EndDate = item.EndDate,
                    DateRangeDisplay = ListingFormat.DateRange(item.StartDate, item.EndDate),
                    DurationDisplay = $"{days} {(days == 1 ? "day" : "days")}",
                    Units = item.Units,
                    Tons = item.Tons,
                    QuantityDisplay = quantityDisplay,
                    MaxUnits = maxUnits,
                    MinRentalDays = minRentalDays,
                    CapacityTons = capacityTons,
                    IsAvailable = isAvailable,
                    AvailabilityMessage = conflictReason,
                    EstimatedGross = itemGross,
                    Note = item.Note,
                    BookingId = item.BookingId,
                    BookingStatus = bookingStatus,
                    BookingCode = bookingCode,
                    CanPay = canPay,
                    PayUrl = payUrl,
                    PassUrl = passUrl,
                    AgreedGross = agreedGross
                });
            }

            var otherDraftPlans = await _harvestPlans.Query()
                .Where(p => p.FarmerId == farmerId && p.Status == HarvestPlanStatus.Draft && p.Id != planId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new HarvestPlanOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    ItemCount = p.Items.Count
                })
                .ToListAsync();

            return new HarvestPlanDetailsViewModel
            {
                Id = plan.Id,
                Name = plan.Name,
                Crop = plan.Crop,
                Note = plan.Note,
                Status = plan.Status,
                CreatedAt = plan.CreatedAt,
                SubmittedOn = plan.SubmittedOn,
                TotalEstimatedGross = liveTotalGross,
                HasConflicts = hasConflicts,
                PendingCount = pendingCount,
                AcceptedCount = acceptedCount,
                PaidCount = paidCount,
                CompletedCount = completedCount,
                RejectedCount = rejectedCount,
                CancelledCount = cancelledCount,
                Items = itemVms,
                OtherDraftPlans = otherDraftPlans
            };
        }

        public async Task<(string? Error, int? PlanId, int? ItemId)> AddItemAsync(string farmerId, HarvestPlanItemInput input)
        {
            if (input.StartDate == null || input.EndDate == null)
            {
                return ("Please select both a start and end date.", null, null);
            }

            var start = input.StartDate.Value.Date;
            var end = input.EndDate.Value.Date;

            if (start < DateTime.Today)
            {
                return ("Start date cannot be in the past.", null, null);
            }
            if (end < start)
            {
                return ("End date must be on or after the start date.", null, null);
            }

            HarvestPlan plan;
            if (input.PlanId.HasValue && input.PlanId.Value > 0)
            {
                var existing = await _harvestPlans.QueryTracked()
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.Id == input.PlanId.Value && p.FarmerId == farmerId);

                if (existing == null)
                {
                    return ("Selected harvest plan not found.", null, null);
                }
                if (existing.Status != HarvestPlanStatus.Draft)
                {
                    return ("You can only add items to draft harvest plans.", null, null);
                }
                plan = existing;
            }
            else
            {
                var draftCount = await _harvestPlans.Query()
                    .CountAsync(p => p.FarmerId == farmerId && p.Status == HarvestPlanStatus.Draft);

                if (draftCount >= MaxDraftPlans)
                {
                    return ($"You can have at most {MaxDraftPlans} draft harvest plans at a time.", null, null);
                }

                var planName = !string.IsNullOrWhiteSpace(input.NewPlanName)
                    ? input.NewPlanName.Trim()
                    : $"Harvest Plan {DateTime.Now:MMM yyyy}";

                plan = new HarvestPlan
                {
                    FarmerId = farmerId,
                    Name = planName,
                    Status = HarvestPlanStatus.Draft,
                    CreatedAt = DateTime.UtcNow
                };
                await _harvestPlans.AddAsync(plan);
                await _harvestPlans.SaveChangesAsync();
            }

            if (plan.Items.Count >= MaxItemsPerPlan)
            {
                return ($"Harvest plans can contain at most {MaxItemsPerPlan} items.", null, null);
            }

            var itemType = input.ItemType ?? (input.EquipmentId.HasValue ? HarvestPlanItemType.Equipment : HarvestPlanItemType.Godown);
            var listingId = input.ListingId > 0
                ? input.ListingId
                : (itemType == HarvestPlanItemType.Equipment ? (input.EquipmentId ?? input.Id) : (input.GodownId ?? input.Id));

            HarvestPlanItem newItem;

            if (itemType == HarvestPlanItemType.Equipment)
            {
                var eq = await _equipment.Query().FirstOrDefaultAsync(e => e.Id == listingId);
                if (eq == null) return ("Equipment listing no longer exists.", null, null);
                if (!eq.IsAvailable) return ("This equipment is currently unavailable for rent.", null, null);
                if (eq.OwnerId == farmerId) return ("You cannot add your own equipment to a harvest plan.", null, null);

                var units = input.Units ?? 1;
                if (units < 1 || units > eq.Quantity)
                {
                    return ($"Please request between 1 and {eq.Quantity} units.", null, null);
                }

                var days = (end - start).Days + 1;
                if (days < eq.MinRentalDays)
                {
                    return ($"This equipment must be rented for at least {eq.MinRentalDays} days.", null, null);
                }

                newItem = new HarvestPlanItem
                {
                    HarvestPlanId = plan.Id,
                    ItemType = HarvestPlanItemType.Equipment,
                    ListingId = eq.Id,
                    StartDate = start,
                    EndDate = end,
                    Units = units,
                    Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
                    AddedAt = DateTime.UtcNow
                };
            }
            else
            {
                var gd = await _godowns.Query().FirstOrDefaultAsync(g => g.Id == listingId);
                if (gd == null) return ("Storage facility no longer exists.", null, null);
                if (!gd.IsActive) return ("This storage facility is currently not accepting bookings.", null, null);
                if (gd.OwnerId == farmerId) return ("You cannot add your own storage facility to a harvest plan.", null, null);

                var tons = input.RequestedCapacityTons ?? input.Tons ?? 0;
                if (tons <= 0) return ("Storage capacity must be greater than zero.", null, null);

                var noteText = input.BookingNotes ?? input.Note;

                newItem = new HarvestPlanItem
                {
                    HarvestPlanId = plan.Id,
                    ItemType = HarvestPlanItemType.Godown,
                    ListingId = gd.Id,
                    StartDate = start,
                    EndDate = end,
                    Tons = tons,
                    Note = string.IsNullOrWhiteSpace(noteText) ? null : noteText.Trim(),
                    AddedAt = DateTime.UtcNow
                };
            }

            await _items.AddAsync(newItem);
            await _items.SaveChangesAsync();

            return (null, plan.Id, newItem.Id);
        }

        public async Task<string?> UpdateItemAsync(string farmerId, int itemId, DateTime start, DateTime end, int? units, double? tons, string? note)
        {
            var item = await _items.QueryTracked()
                .Include(i => i.Plan)
                .FirstOrDefaultAsync(i => i.Id == itemId && i.Plan!.FarmerId == farmerId);

            if (item == null) return "Harvest plan item not found.";
            if (item.Plan!.Status != HarvestPlanStatus.Draft) return "Only items in draft plans can be updated.";

            var s = start.Date;
            var t = end.Date;
            if (s < DateTime.Today) return "Start date cannot be in the past.";
            if (t < s) return "End date must be on or after start date.";

            item.StartDate = s;
            item.EndDate = t;
            item.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            if (item.ItemType == HarvestPlanItemType.Equipment)
            {
                var eq = await _equipment.GetByIdAsync(item.ListingId);
                if (eq == null) return "Equipment listing not found.";

                var u = units ?? item.Units;
                if (u < 1 || u > eq.Quantity) return $"Units must be between 1 and {eq.Quantity}.";

                var days = (t - s).Days + 1;
                if (days < eq.MinRentalDays) return $"This equipment must be rented for at least {eq.MinRentalDays} days.";

                item.Units = u;
            }
            else
            {
                var gd = await _godowns.GetByIdAsync(item.ListingId);
                if (gd == null) return "Storage facility not found.";

                var storageTons = tons ?? item.Tons;
                if (storageTons <= 0) return "Storage tons must be greater than zero.";

                item.Tons = storageTons;
            }

            await _items.SaveChangesAsync();
            return null;
        }

        public async Task<string?> RemoveItemAsync(string farmerId, int itemId)
        {
            var item = await _items.QueryTracked()
                .Include(i => i.Plan)
                .FirstOrDefaultAsync(i => i.Id == itemId && i.Plan!.FarmerId == farmerId);

            if (item == null) return "Item not found.";
            if (item.Plan!.Status != HarvestPlanStatus.Draft) return "Only items in draft plans can be removed.";

            _items.Remove(item);
            await _items.SaveChangesAsync();
            return null;
        }

        public async Task<string?> UpdatePlanAsync(string farmerId, int planId, string name, string? crop = null, string? note = null)
        {
            var plan = await _harvestPlans.QueryTracked()
                .FirstOrDefaultAsync(p => p.Id == planId && p.FarmerId == farmerId);

            if (plan == null) return "Plan not found.";
            if (plan.Status != HarvestPlanStatus.Draft) return "Only draft plans can be updated.";
            if (string.IsNullOrWhiteSpace(name)) return "Plan name is required.";

            plan.Name = name.Trim();
            if (crop != null) plan.Crop = string.IsNullOrWhiteSpace(crop) ? null : crop.Trim();
            if (note != null) plan.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            await _harvestPlans.SaveChangesAsync();
            return null;
        }

        public async Task<string?> DeleteAsync(string farmerId, int planId)
        {
            var plan = await _harvestPlans.QueryTracked()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == planId && p.FarmerId == farmerId);

            if (plan == null) return "Plan not found.";
            if (plan.Status == HarvestPlanStatus.Submitted)
            {
                return "Cannot delete a submitted harvest plan with active booking requests.";
            }

            _harvestPlans.Remove(plan);
            await _harvestPlans.SaveChangesAsync();
            return null;
        }

        public async Task<HarvestPlanSubmitResult> SubmitAsync(string farmerId, int planId)
        {
            var plan = await _harvestPlans.QueryTracked()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == planId && p.FarmerId == farmerId);

            if (plan == null) return HarvestPlanSubmitResult.Fail("Harvest plan not found.");
            if (plan.Status != HarvestPlanStatus.Draft) return HarvestPlanSubmitResult.Fail("Only draft harvest plans can be submitted.");
            if (plan.Items.Count == 0) return HarvestPlanSubmitResult.Fail("Cannot submit an empty harvest plan.");

            var unsubmitted = plan.Items
                .Where(i => !i.BookingId.HasValue)
                .ToList();

            if (unsubmitted.Count == 0)
            {
                plan.Status = HarvestPlanStatus.Submitted;
                plan.SubmittedOn ??= DateTime.UtcNow;
                await _harvestPlans.SaveChangesAsync();
                return HarvestPlanSubmitResult.Ok(plan.Items.Count, 0, plan.Id);
            }

            var eqIds = unsubmitted.Where(i => i.ItemType == HarvestPlanItemType.Equipment).Select(i => i.ListingId).Distinct().ToList();
            var gdIds = unsubmitted.Where(i => i.ItemType == HarvestPlanItemType.Godown).Select(i => i.ListingId).Distinct().ToList();

            var equipments = await _equipment.Query().Where(e => eqIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
            var godowns = await _godowns.Query().Where(g => gdIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id);

            // 1. Validation pass (read-only, fail-fast)
            var validationErrors = new List<string>();
            foreach (var item in unsubmitted)
            {
                if (item.ItemType == HarvestPlanItemType.Equipment)
                {
                    if (!equipments.TryGetValue(item.ListingId, out var eq) || !eq.IsAvailable)
                    {
                        validationErrors.Add($"Equipment #{item.ListingId}: Listing is currently unavailable.");
                    }
                    else
                    {
                        var clash = await _equipmentService.CheckAvailabilityAsync(eq.Id, item.StartDate, item.EndDate, item.Units);
                        if (clash != null)
                        {
                            validationErrors.Add($"{eq.Name}: {clash}");
                        }
                    }
                }
                else
                {
                    if (!godowns.TryGetValue(item.ListingId, out var gd) || !gd.IsActive)
                    {
                        validationErrors.Add($"Godown #{item.ListingId}: Storage facility is not active.");
                    }
                    else
                    {
                        var clash = await _godownService.CheckAvailabilityAsync(gd.Id, item.Tons, item.StartDate, item.EndDate);
                        if (clash != null)
                        {
                            validationErrors.Add($"{gd.Name}: {clash}");
                        }
                    }
                }
            }

            if (validationErrors.Count > 0)
            {
                return HarvestPlanSubmitResult.Partial(0, validationErrors.Count, "Some items are not available. Please adjust dates or quantities and try again.", validationErrors, plan.Id);
            }

            // 2. Booking creation pass
            int createdCount = 0;
            int failedCount = 0;
            var creationErrors = new List<string>();

            foreach (var item in unsubmitted)
            {
                if (item.ItemType == HarvestPlanItemType.Equipment)
                {
                    var eq = equipments[item.ListingId];
                    var (err, bookingId) = await _equipmentService.RequestRentalWithResultAsync(
                        farmerId,
                        item.ListingId,
                        item.StartDate,
                        item.EndDate,
                        item.Note,
                        item.Units,
                        promoCode: null,
                        pointsToRedeem: null,
                        harvestPlanId: plan.Id,
                        planName: plan.Name);

                    if (err != null || !bookingId.HasValue)
                    {
                        failedCount++;
                        creationErrors.Add($"{eq.Name}: {err ?? "Booking request failed"}");
                    }
                    else
                    {
                        item.BookingId = bookingId.Value;
                        createdCount++;
                    }
                }
                else
                {
                    var gd = godowns[item.ListingId];
                    var detailVm = new GodownDetailViewModel
                    {
                        Id = item.ListingId,
                        StartDate = item.StartDate,
                        EndDate = item.EndDate,
                        RequestedCapacityTons = item.Tons,
                        BookingNotes = item.Note
                    };

                    var (err, bookingId) = await _godownService.RequestStorageWithResultAsync(
                        farmerId,
                        detailVm,
                        promoCode: null,
                        pointsToRedeem: null,
                        harvestPlanId: plan.Id,
                        planName: plan.Name);

                    if (err != null || !bookingId.HasValue)
                    {
                        failedCount++;
                        creationErrors.Add($"{gd.Name}: {err ?? "Storage request failed"}");
                    }
                    else
                    {
                        item.BookingId = bookingId.Value;
                        createdCount++;
                    }
                }
            }

            if (failedCount == 0)
            {
                plan.Status = HarvestPlanStatus.Submitted;
                plan.SubmittedOn = DateTime.UtcNow;
                await _harvestPlans.SaveChangesAsync();

                // Farmer summary notification
                await _notifications.NotifyAsync(new NotificationRequest
                {
                    UserId = farmerId,
                    Type = NotificationTypes.System,
                    TitleKey = "Harvest Plan Submitted",
                    MessageKey = "{0}: {1} requests sent to owners.",
                    Args = new object[] { plan.Name, plan.Items.Count },
                    LinkUrl = AppLinks.HarvestPlan(plan.Id),
                    DedupeKey = $"harvestplan:{plan.Id}:Submitted",
                    SendEmail = false
                });

                return HarvestPlanSubmitResult.Ok(createdCount, 0, plan.Id);
            }
            else
            {
                // Partial failure: save created items, remain Draft
                await _harvestPlans.SaveChangesAsync();
                var msg = $"{createdCount} of {unsubmitted.Count} requests submitted. Some items were no longer available — update dates or quantities and submit again.";
                return HarvestPlanSubmitResult.Partial(createdCount, failedCount, msg, creationErrors, plan.Id);
            }
        }

        public async Task<(string? Error, int? NewPlanId)> CloneAsync(string farmerId, int planId, int shiftDays = 365)
        {
            var draftCount = await _harvestPlans.Query()
                .CountAsync(p => p.FarmerId == farmerId && p.Status == HarvestPlanStatus.Draft);

            if (draftCount >= MaxDraftPlans)
            {
                return ($"You already have {MaxDraftPlans} draft harvest plans. Delete or submit one before cloning.", null);
            }

            if (shiftDays < 1 || shiftDays > 730)
            {
                shiftDays = 365;
            }

            var source = await _harvestPlans.Query()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == planId && p.FarmerId == farmerId);

            if (source == null) return ("Harvest plan not found.", null);
            if (source.Items.Count == 0) return ("Cannot clone an empty harvest plan.", null);

            var clone = new HarvestPlan
            {
                FarmerId = farmerId,
                Name = $"{source.Name} (Copy)",
                Crop = source.Crop,
                Note = source.Note,
                Status = HarvestPlanStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                Items = source.Items.Select(i => new HarvestPlanItem
                {
                    ItemType = i.ItemType,
                    ListingId = i.ListingId,
                    StartDate = i.StartDate.AddDays(shiftDays),
                    EndDate = i.EndDate.AddDays(shiftDays),
                    Units = i.Units,
                    Tons = i.Tons,
                    Note = i.Note,
                    BookingId = null,
                    AddedAt = DateTime.UtcNow
                }).ToList()
            };

            await _harvestPlans.AddAsync(clone);
            await _harvestPlans.SaveChangesAsync();

            return (null, clone.Id);
        }

        public async Task<int> CountDraftItemsAsync(string farmerId)
        {
            return await _items.Query()
                .CountAsync(i => i.Plan!.FarmerId == farmerId && i.Plan.Status == HarvestPlanStatus.Draft);
        }

        public async Task<List<HarvestPlanOptionViewModel>> GetDraftPlansForSelectionAsync(string farmerId)
        {
            return await _harvestPlans.Query()
                .Where(p => p.FarmerId == farmerId && p.Status == HarvestPlanStatus.Draft)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new HarvestPlanOptionViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    ItemCount = p.Items.Count
                })
                .ToListAsync();
        }
    }
}
