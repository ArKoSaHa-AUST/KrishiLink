using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace KrishiLink.BLL.Services
{
    public interface IStorageIntakeService
    {
        Task<List<StorageIntakeLotItemViewModel>> GetForBookingAsync(string ownerId, int bookingId);
        Task<List<StorageIntakeLotItemViewModel>> GetForFarmerBookingAsync(string farmerId, int bookingId);
        Task<(string? Error, int? LotId)> RecordAsync(string ownerId, IntakeLotInput input);
        Task<string?> UpdateAsync(string ownerId, int lotId, IntakeLotInput input);
        Task<string?> DeleteAsync(string ownerId, int lotId);
        Task<string?> ReleaseAsync(string ownerId, int lotId, string releasedTo, string? remarks);
        Task<(byte[] Content, string FileName)?> GetReceiptPdfAsync(int lotId, string requesterId, bool isOwner);
        Task<ReceiptVerificationViewModel> GetVerificationAsync(string receiptNumber);
        Task<Dictionary<int, IntakeSummary>> SummariseAsync(IEnumerable<int> bookingIds);
    }

    public class StorageIntakeService : IStorageIntakeService
    {
        private readonly IRepository<StorageIntakeLot> _lots;
        private readonly IRepository<GodownBooking> _bookings;
        private readonly IQrCodeService _qrCode;
        private readonly INotificationService _notifications;
        private readonly AppOptions _appOptions;
        private readonly ILogger<StorageIntakeService> _logger;

        public StorageIntakeService(
            IRepository<StorageIntakeLot> lots,
            IRepository<GodownBooking> bookings,
            IQrCodeService qrCode,
            INotificationService notifications,
            IOptions<AppOptions> appOptions,
            ILogger<StorageIntakeService> logger)
        {
            _lots = lots;
            _bookings = bookings;
            _qrCode = qrCode;
            _notifications = notifications;
            _appOptions = appOptions.Value;
            _logger = logger;
        }

        public async Task<List<StorageIntakeLotItemViewModel>> GetForBookingAsync(string ownerId, int bookingId)
        {
            var isOwner = await _bookings.Query()
                .AnyAsync(b => b.Id == bookingId && b.Godown!.OwnerId == ownerId);
            if (!isOwner) return new List<StorageIntakeLotItemViewModel>();

            var lots = await _lots.Query()
                .Where(l => l.GodownBookingId == bookingId)
                .OrderByDescending(l => l.IntakeDate)
                .ThenByDescending(l => l.Id)
                .ToListAsync();

            return lots.Select(ToViewModel).ToList();
        }

        public async Task<List<StorageIntakeLotItemViewModel>> GetForFarmerBookingAsync(string farmerId, int bookingId)
        {
            var isFarmer = await _bookings.Query()
                .AnyAsync(b => b.Id == bookingId && b.FarmerId == farmerId);
            if (!isFarmer) return new List<StorageIntakeLotItemViewModel>();

            var lots = await _lots.Query()
                .Where(l => l.GodownBookingId == bookingId)
                .OrderByDescending(l => l.IntakeDate)
                .ThenByDescending(l => l.Id)
                .ToListAsync();

            return lots.Select(ToViewModel).ToList();
        }

        public async Task<(string? Error, int? LotId)> RecordAsync(string ownerId, IntakeLotInput input)
        {
            var booking = await _bookings.Query()
                .Include(b => b.Godown)
                .Include(b => b.Farmer)
                .FirstOrDefaultAsync(b => b.Id == input.GodownBookingId && b.Godown!.OwnerId == ownerId);

            if (booking is null)
                return ("Storage booking not found or unauthorized.", null);

            var validationErr = ValidateIntakePermissionsAndData(booking, input);
            if (validationErr is not null)
                return (validationErr, null);

            // Over-storage guard: total Stored lots + new lot <= StorageTons * 1000 * 1.05
            var existingStoredKg = await _lots.Query()
                .Where(l => l.GodownBookingId == booking.Id && l.Status == IntakeLotStatus.Stored)
                .SumAsync(l => (decimal?)l.NetWeightKg) ?? 0m;

            var maxAllowedKg = (decimal)(booking.StorageTons * 1000.0 * 1.05);
            if (existingStoredKg + input.NetWeightKg > maxAllowedKg)
            {
                var totalStoredTons = (existingStoredKg + input.NetWeightKg) / 1000m;
                return ($"Total stored ({totalStoredTons:N2} t) exceeds the booked {booking.StorageTons:N0} t. Ask the farmer to increase the booked tonnage.", null);
            }

            var cleanGrade = string.IsNullOrWhiteSpace(input.Grade) ? IntakeGrades.Ungraded : input.Grade.Trim();
            if (!IntakeGrades.All.Contains(cleanGrade, StringComparer.OrdinalIgnoreCase))
                cleanGrade = IntakeGrades.Ungraded;

            var lot = new StorageIntakeLot
            {
                GodownBookingId = booking.Id,
                ReceiptNumber = $"TMP-{Guid.NewGuid():N}",
                IntakeDate = input.IntakeDate.Date,
                Crop = input.Crop.Trim(),
                Variety = string.IsNullOrWhiteSpace(input.Variety) ? null : input.Variety.Trim(),
                Bags = input.Bags,
                BagWeightKg = input.BagWeightKg,
                NetWeightKg = input.NetWeightKg,
                MoisturePercent = input.MoisturePercent,
                Grade = cleanGrade,
                Remarks = string.IsNullOrWhiteSpace(input.Remarks) ? null : input.Remarks.Trim(),
                Status = IntakeLotStatus.Stored,
                RecordedByUserId = ownerId,
                RecordedAt = DateTime.UtcNow
            };

            await _lots.AddAsync(lot);
            await _lots.SaveChangesAsync();

            // Two-step save to assign canonical receipt number: "KL-WR-{yyyy}-{Id:D5}"
            lot.ReceiptNumber = $"KL-WR-{lot.IntakeDate.Year:D4}-{lot.Id:D5}";
            await _lots.SaveChangesAsync();

            // Notification to farmer
            if (booking.Farmer != null)
            {
                var ownerName = booking.Godown?.Owner?.FullName ?? "The godown owner";
                var godownName = booking.Godown?.Name ?? "the godown";

                await _notifications.NotifyAsync(new NotificationRequest
                {
                    UserId = booking.FarmerId,
                    Type = NotificationTypes.System,
                    TitleKey = "Warehouse receipt issued",
                    MessageKey = "{0} recorded {1} bags of {2} ({3} kg) at {4}. Receipt {5}.",
                    Args = new object[]
                    {
                        ownerName,
                        lot.Bags,
                        lot.Crop,
                        lot.NetWeightKg.ToString("N0"),
                        godownName,
                        lot.ReceiptNumber
                    },
                    LinkUrl = AppLinks.FarmerBookings("Godown", booking.Id),
                    DedupeKey = $"intake:{lot.Id}:recorded",
                    SendEmail = true,
                    RecipientEmail = booking.Farmer.Email
                });
            }

            return (null, lot.Id);
        }

        public async Task<string?> UpdateAsync(string ownerId, int lotId, IntakeLotInput input)
        {
            var lot = await _lots.QueryTracked()
                .Include(l => l.Booking)
                    .ThenInclude(b => b!.Godown)
                .FirstOrDefaultAsync(l => l.Id == lotId && l.Booking!.Godown!.OwnerId == ownerId);

            if (lot is null)
                return "Storage intake lot not found or unauthorized.";

            if (lot.Status == IntakeLotStatus.Released)
                return "Released intake lots cannot be modified.";

            var booking = lot.Booking!;
            var validationErr = ValidateIntakePermissionsAndData(booking, input);
            if (validationErr is not null)
                return validationErr;

            // Over-storage guard (excluding current lot)
            var otherStoredKg = await _lots.Query()
                .Where(l => l.GodownBookingId == booking.Id && l.Id != lot.Id && l.Status == IntakeLotStatus.Stored)
                .SumAsync(l => (decimal?)l.NetWeightKg) ?? 0m;

            var maxAllowedKg = (decimal)(booking.StorageTons * 1000.0 * 1.05);
            if (otherStoredKg + input.NetWeightKg > maxAllowedKg)
            {
                var totalStoredTons = (otherStoredKg + input.NetWeightKg) / 1000m;
                return $"Total stored ({totalStoredTons:N2} t) exceeds the booked {booking.StorageTons:N0} t. Ask the farmer to increase the booked tonnage.";
            }

            var cleanGrade = string.IsNullOrWhiteSpace(input.Grade) ? IntakeGrades.Ungraded : input.Grade.Trim();
            if (!IntakeGrades.All.Contains(cleanGrade, StringComparer.OrdinalIgnoreCase))
                cleanGrade = IntakeGrades.Ungraded;

            lot.IntakeDate = input.IntakeDate.Date;
            lot.Crop = input.Crop.Trim();
            lot.Variety = string.IsNullOrWhiteSpace(input.Variety) ? null : input.Variety.Trim();
            lot.Bags = input.Bags;
            lot.BagWeightKg = input.BagWeightKg;
            lot.NetWeightKg = input.NetWeightKg;
            lot.MoisturePercent = input.MoisturePercent;
            lot.Grade = cleanGrade;
            lot.Remarks = string.IsNullOrWhiteSpace(input.Remarks) ? null : input.Remarks.Trim();
            lot.UpdatedAt = DateTime.UtcNow;

            await _lots.SaveChangesAsync();
            return null;
        }

        public async Task<string?> DeleteAsync(string ownerId, int lotId)
        {
            var lot = await _lots.QueryTracked()
                .Include(l => l.Booking)
                    .ThenInclude(b => b!.Godown)
                .FirstOrDefaultAsync(l => l.Id == lotId && l.Booking!.Godown!.OwnerId == ownerId);

            if (lot is null)
                return "Storage intake lot not found or unauthorized.";

            if (lot.Status == IntakeLotStatus.Released)
                return "Released intake lots cannot be deleted.";

            _lots.Remove(lot);
            await _lots.SaveChangesAsync();

            _logger.LogInformation("Intake lot {LotId} deleted by {OwnerId}", lotId, ownerId);
            return null;
        }

        public async Task<string?> ReleaseAsync(string ownerId, int lotId, string releasedTo, string? remarks)
        {
            if (string.IsNullOrWhiteSpace(releasedTo))
                return "Please specify the person or representative collecting the produce.";

            var lot = await _lots.QueryTracked()
                .Include(l => l.Booking)
                    .ThenInclude(b => b!.Godown)
                        .ThenInclude(g => g!.Owner)
                .Include(l => l.Booking)
                    .ThenInclude(b => b!.Farmer)
                .FirstOrDefaultAsync(l => l.Id == lotId && l.Booking!.Godown!.OwnerId == ownerId);

            if (lot is null)
                return "Storage intake lot not found or unauthorized.";

            if (lot.Status == IntakeLotStatus.Released)
                return "This intake lot has already been released.";

            lot.Status = IntakeLotStatus.Released;
            lot.ReleasedOn = DateTime.UtcNow;
            lot.ReleasedTo = releasedTo.Trim();
            lot.ReleaseRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
            lot.UpdatedAt = DateTime.UtcNow;

            await _lots.SaveChangesAsync();

            // Notify farmer of release
            var booking = lot.Booking!;
            if (booking.Farmer != null)
            {
                var ownerName = booking.Godown?.Owner?.FullName ?? "The godown owner";

                await _notifications.NotifyAsync(new NotificationRequest
                {
                    UserId = booking.FarmerId,
                    Type = NotificationTypes.System,
                    TitleKey = "Goods released",
                    MessageKey = "{0} released {1} bags of {2} (receipt {3}) to {4}.",
                    Args = new object[]
                    {
                        ownerName,
                        lot.Bags,
                        lot.Crop,
                        lot.ReceiptNumber,
                        lot.ReleasedTo
                    },
                    LinkUrl = AppLinks.FarmerBookings("Godown", booking.Id),
                    DedupeKey = $"intake:{lot.Id}:released",
                    SendEmail = true,
                    RecipientEmail = booking.Farmer.Email
                });
            }

            return null;
        }

        public async Task<(byte[] Content, string FileName)?> GetReceiptPdfAsync(int lotId, string requesterId, bool isOwner)
        {
            var lot = await _lots.Query()
                .Include(l => l.Booking!)
                    .ThenInclude(b => b.Godown!)
                        .ThenInclude(g => g.Owner)
                .Include(l => l.Booking!)
                    .ThenInclude(b => b.Farmer)
                .FirstOrDefaultAsync(l => l.Id == lotId);

            if (lot == null || lot.Booking == null || lot.Booking.Godown == null || lot.Booking.Farmer == null)
                return null;

            var booking = lot.Booking;
            var godown = booking.Godown;
            var farmer = booking.Farmer;
            var owner = godown.Owner;

            // Authorization check
            if (isOwner)
            {
                if (godown.OwnerId != requesterId) return null;
            }
            else
            {
                if (booking.FarmerId != requesterId) return null;
            }

            var verifyUrl = $"{_appOptions.PublicBaseUrl.TrimEnd('/')}/Verify/Receipt/{lot.ReceiptNumber}";
            var qrB64 = _qrCode.GenerateBase64Png(verifyUrl, 6);
            var rawB64 = qrB64.Contains(',') ? qrB64.Split(',')[1] : qrB64;
            var qrBytes = Convert.FromBase64String(rawB64);

            var model = new WarehouseReceiptModel(
                ReceiptNumber: lot.ReceiptNumber,
                Status: lot.Status,
                IntakeDate: lot.IntakeDate,
                ReleasedOn: lot.ReleasedOn,
                ReleasedTo: lot.ReleasedTo,
                ReleaseRemarks: lot.ReleaseRemarks,
                GodownName: godown.Name,
                StorageType: godown.StorageType,
                GodownLocation: godown.Location,
                GodownDistrict: godown.District,
                OwnerName: owner?.FullName ?? "Godown Owner",
                OwnerBusiness: owner?.BusinessOrFarmName,
                OwnerPhone: owner?.PhoneNumber ?? "—",
                FarmerName: farmer.FullName ?? "Farmer",
                FarmerLocation: farmer.Location ?? "—",
                FarmerPhone: farmer.PhoneNumber ?? "—",
                Crop: lot.Crop,
                Variety: lot.Variety,
                Bags: lot.Bags,
                BagWeightKg: lot.BagWeightKg,
                NetWeightKg: lot.NetWeightKg,
                MoisturePercent: lot.MoisturePercent,
                Grade: lot.Grade,
                Remarks: lot.Remarks,
                BookingCode: $"KL-GD-{booking.RequestedOn.Year}-{booking.Id:D3}",
                BookedStorageTons: booking.StorageTons,
                BookingStartDate: booking.StartDate,
                BookingEndDate: booking.EndDate,
                AgreedRatePerTonMonth: booking.AgreedRate ?? godown.PricePerTonPerMonth,
                VerifyUrl: verifyUrl,
                QrCodePngBytes: qrBytes
            );

            var document = new WarehouseReceiptDocument(model);
            return (document.GeneratePdf(), document.FileName);
        }

        public async Task<ReceiptVerificationViewModel> GetVerificationAsync(string receiptNumber)
        {
            if (string.IsNullOrWhiteSpace(receiptNumber))
            {
                return new ReceiptVerificationViewModel { IsFound = false };
            }

            var clean = receiptNumber.Trim();
            var lot = await _lots.Query()
                .Include(l => l.Booking!)
                    .ThenInclude(b => b.Godown)
                .FirstOrDefaultAsync(l => l.ReceiptNumber == clean);

            if (lot == null || lot.Booking == null || lot.Booking.Godown == null)
            {
                return new ReceiptVerificationViewModel
                {
                    IsFound = false,
                    ReceiptNumber = clean
                };
            }

            var godown = lot.Booking.Godown;
            return new ReceiptVerificationViewModel
            {
                IsFound = true,
                ReceiptNumber = lot.ReceiptNumber,
                Status = lot.Status,
                GodownName = godown.Name,
                StorageType = godown.StorageType,
                District = godown.District ?? string.Empty,
                Location = godown.Location,
                Crop = lot.Crop,
                Variety = lot.Variety,
                Bags = lot.Bags,
                BagWeightKg = lot.BagWeightKg,
                NetWeightKg = lot.NetWeightKg,
                MoisturePercent = lot.MoisturePercent,
                Grade = lot.Grade,
                Remarks = lot.Remarks,
                IntakeDate = lot.IntakeDate,
                ReleasedOn = lot.ReleasedOn,
                ReleasedTo = lot.ReleasedTo,
                ReleaseRemarks = lot.ReleaseRemarks,
                VerificationUrl = $"{_appOptions.PublicBaseUrl.TrimEnd('/')}/Verify/Receipt/{lot.ReceiptNumber}"
            };
        }

        public async Task<Dictionary<int, IntakeSummary>> SummariseAsync(IEnumerable<int> bookingIds)
        {
            var ids = bookingIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, IntakeSummary>();

            var rows = await _lots.Query()
                .Where(l => ids.Contains(l.GodownBookingId))
                .GroupBy(l => l.GodownBookingId)
                .Select(g => new
                {
                    BookingId = g.Key,
                    StoredKg = g.Where(l => l.Status == IntakeLotStatus.Stored).Sum(l => l.NetWeightKg),
                    ReleasedKg = g.Where(l => l.Status == IntakeLotStatus.Released).Sum(l => l.NetWeightKg),
                    Count = g.Count(),
                    LastIntake = g.Max(l => (DateTime?)l.IntakeDate)
                })
                .ToListAsync();

            return rows.ToDictionary(
                r => r.BookingId,
                r => new IntakeSummary(r.StoredKg, r.ReleasedKg, r.Count, r.LastIntake));
        }

        private static string? ValidateIntakePermissionsAndData(GodownBooking booking, IntakeLotInput input)
        {
            if (booking.Status != BookingStatus.Paid && booking.Status != BookingStatus.Completed)
                return "Intake lots can only be recorded for Paid or Completed storage bookings.";

            if (booking.Status == BookingStatus.Completed)
            {
                var completionDate = booking.CompletedOn ?? booking.UpdatedOn;
                if (completionDate.HasValue && completionDate.Value.AddDays(30) < DateTime.UtcNow)
                    return "Late intake entries are not allowed after 30 days of completion.";
            }

            var minDate = booking.StartDate.Date.AddDays(-3);
            var maxDate = booking.EndDate.Date.AddDays(7);
            var intakeDate = input.IntakeDate.Date;

            if (intakeDate < minDate || intakeDate > maxDate)
                return $"Intake date must fall within 3 days before start ({minDate:dd MMM yyyy}) and 7 days after end ({maxDate:dd MMM yyyy}).";

            if (intakeDate > DateTime.Today)
                return "Intake date cannot be in the future.";

            if (string.IsNullOrWhiteSpace(input.Crop))
                return "Crop name is required.";

            if (input.Bags < 1 || input.Bags > 100000)
                return "Bags count must be between 1 and 100,000.";

            if (input.BagWeightKg < 1m || input.BagWeightKg > 200m)
                return "Bag weight must be between 1 and 200 kg.";

            var maxToleranceNetKg = input.Bags * input.BagWeightKg * 1.10m;
            if (input.NetWeightKg <= 0m || input.NetWeightKg > maxToleranceNetKg)
                return $"Net weight must be greater than 0 and cannot exceed {maxToleranceNetKg:N1} kg (including 10% weighbridge tolerance).";

            if (input.MoisturePercent.HasValue && (input.MoisturePercent.Value < 0m || input.MoisturePercent.Value > 40m))
                return "Moisture percentage must be between 0% and 40%.";

            return null;
        }

        private static StorageIntakeLotItemViewModel ToViewModel(StorageIntakeLot l) => new()
        {
            Id = l.Id,
            GodownBookingId = l.GodownBookingId,
            ReceiptNumber = l.ReceiptNumber,
            IntakeDate = l.IntakeDate,
            Crop = l.Crop,
            Variety = l.Variety,
            Bags = l.Bags,
            BagWeightKg = l.BagWeightKg,
            NetWeightKg = l.NetWeightKg,
            MoisturePercent = l.MoisturePercent,
            Grade = l.Grade,
            Remarks = l.Remarks,
            Status = l.Status,
            ReleasedOn = l.ReleasedOn,
            ReleasedTo = l.ReleasedTo,
            ReleaseRemarks = l.ReleaseRemarks,
            RecordedAt = l.RecordedAt,
            UpdatedAt = l.UpdatedAt,
            ReceiptPdfUrl = AppLinks.WarehouseReceipt(l.Id)
        };
    }
}
