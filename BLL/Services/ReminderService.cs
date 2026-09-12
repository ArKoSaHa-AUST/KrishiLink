using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services
{
    public record ReminderRunSummary(int Sent, int Skipped);

    public interface IReminderService
    {
        Task<ReminderRunSummary> SendDueRemindersAsync(CancellationToken ct = default, string? onlyUserId = null);
    }

    public class ReminderService : IReminderService
    {
        private readonly IRepository<EquipmentBooking> _equipmentBookings;
        private readonly IRepository<GodownBooking> _godownBookings;
        private readonly INotificationService _notifications;
        private readonly IOptions<ReminderOptions> _options;
        private readonly ILogger<ReminderService> _logger;

        public ReminderService(
            IRepository<EquipmentBooking> equipmentBookings,
            IRepository<GodownBooking> godownBookings,
            INotificationService notifications,
            IOptions<ReminderOptions> options,
            ILogger<ReminderService> logger)
        {
            _equipmentBookings = equipmentBookings;
            _godownBookings = godownBookings;
            _notifications = notifications;
            _options = options;
            _logger = logger;
        }

        public async Task<ReminderRunSummary> SendDueRemindersAsync(CancellationToken ct = default, string? onlyUserId = null)
        {
            var totalSent = 0;
            var totalSkipped = 0;

            var rules = new Func<CancellationToken, string?, Task<(int Sent, int Skipped)>>[]
            {
                RunRule1StartsTomorrowFarmerAsync,
                RunRule2StartsTomorrowOwnerAsync,
                RunRule3ReturnDueFarmerAsync,
                RunRule4StorageEndingFarmerAsync,
                RunRule5PaymentPendingFarmerAsync,
                RunRule6OverdueCompletionOwnerAsync,
                RunRule7StalePendingOwnerAsync
            };

            foreach (var rule in rules)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var (sent, skipped) = await rule(ct, onlyUserId);
                    totalSent += sent;
                    totalSkipped += skipped;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Reminder rule {RuleMethod} failed during execution.", rule.Method.Name);
                }
            }

            _logger.LogInformation("Reminder run: {Sent} sent, {Skipped} already delivered.", totalSent, totalSkipped);
            return new ReminderRunSummary(totalSent, totalSkipped);
        }

        // ---------------------------------------------------------------- Rule Implementations

        /// <summary>
        /// R1: Farmer — Equipment or Godown, Status == Paid, today < StartDate <= today + 1
        /// </summary>
        private async Task<(int Sent, int Skipped)> RunRule1StartsTomorrowFarmerAsync(CancellationToken ct, string? onlyUserId)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var eqQuery = _equipmentBookings.Query()
                .Include(b => b.Equipment!).ThenInclude(e => e.Owner)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Paid && b.StartDate.Date > today && b.StartDate.Date <= tomorrow);

            var gdQuery = _godownBookings.Query()
                .Include(b => b.Godown!).ThenInclude(g => g.Owner)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Paid && b.StartDate.Date > today && b.StartDate.Date <= tomorrow);

            if (!string.IsNullOrEmpty(onlyUserId))
            {
                eqQuery = eqQuery.Where(b => b.FarmerId == onlyUserId);
                gdQuery = gdQuery.Where(b => b.FarmerId == onlyUserId);
            }

            var eqBookings = await eqQuery.ToListAsync(ct);
            var gdBookings = await gdQuery.ToListAsync(ct);

            var requests = new List<NotificationRequest>();

            foreach (var b in eqBookings)
            {
                requests.Add(BuildFarmerRequest(
                    userId: b.FarmerId,
                    titleKey: "Your booking starts tomorrow",
                    messageKey: "{0} starts tomorrow ({1}). Owner: {2}, {3}.",
                    args: new object[]
                    {
                        b.Equipment?.Name ?? "Equipment",
                        $"{b.StartDate:dd MMM yyyy}",
                        b.Equipment?.Owner?.FullName ?? "Owner",
                        b.Equipment?.Owner?.PhoneNumber ?? "—"
                    },
                    linkUrl: AppLinks.FarmerBookings("equipment", b.Id),
                    dedupeKey: $"reminder:equipment:{b.Id}:start:{b.ModificationCount}",
                    recipientEmail: b.Farmer?.Email));
            }

            foreach (var b in gdBookings)
            {
                requests.Add(BuildFarmerRequest(
                    userId: b.FarmerId,
                    titleKey: "Your booking starts tomorrow",
                    messageKey: "{0} starts tomorrow ({1}). Owner: {2}, {3}.",
                    args: new object[]
                    {
                        b.Godown?.Name ?? "Godown",
                        $"{b.StartDate:dd MMM yyyy}",
                        b.Godown?.Owner?.FullName ?? "Owner",
                        b.Godown?.Owner?.PhoneNumber ?? "—"
                    },
                    linkUrl: AppLinks.FarmerBookings("godown", b.Id),
                    dedupeKey: $"reminder:godown:{b.Id}:start:{b.ModificationCount}",
                    recipientEmail: b.Farmer?.Email));
            }

            return await NotifyManyAsync(requests, ct);
        }

        /// <summary>
        /// R2: Owner — Equipment or Godown, Status == Paid, today < StartDate <= today + 1
        /// </summary>
        private async Task<(int Sent, int Skipped)> RunRule2StartsTomorrowOwnerAsync(CancellationToken ct, string? onlyUserId)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var eqQuery = _equipmentBookings.Query()
                .Include(b => b.Equipment)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Paid && b.StartDate.Date > today && b.StartDate.Date <= tomorrow && b.Equipment != null && !string.IsNullOrEmpty(b.Equipment.OwnerId));

            var gdQuery = _godownBookings.Query()
                .Include(b => b.Godown)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Paid && b.StartDate.Date > today && b.StartDate.Date <= tomorrow && b.Godown != null && !string.IsNullOrEmpty(b.Godown.OwnerId));

            if (!string.IsNullOrEmpty(onlyUserId))
            {
                eqQuery = eqQuery.Where(b => b.Equipment!.OwnerId == onlyUserId);
                gdQuery = gdQuery.Where(b => b.Godown!.OwnerId == onlyUserId);
            }

            var eqBookings = await eqQuery.ToListAsync(ct);
            var gdBookings = await gdQuery.ToListAsync(ct);

            var requests = new List<NotificationRequest>();

            foreach (var b in eqBookings)
            {
                requests.Add(BuildOwnerRequest(
                    userId: b.Equipment!.OwnerId,
                    titleKey: "Handover tomorrow",
                    messageKey: "{0} for {1} starts tomorrow ({2}). Please prepare the handover.",
                    args: new object[]
                    {
                        b.Equipment?.Name ?? "Equipment",
                        b.Farmer?.FullName ?? "A farmer",
                        $"{b.StartDate:dd MMM yyyy}"
                    },
                    linkUrl: AppLinks.OwnerRequests("equipment", b.Id),
                    dedupeKey: $"reminder:equipment:{b.Id}:owner-start:{b.ModificationCount}"));
            }

            foreach (var b in gdBookings)
            {
                requests.Add(BuildOwnerRequest(
                    userId: b.Godown!.OwnerId,
                    titleKey: "Handover tomorrow",
                    messageKey: "{0} for {1} starts tomorrow ({2}). Please prepare the handover.",
                    args: new object[]
                    {
                        b.Godown?.Name ?? "Godown",
                        b.Farmer?.FullName ?? "A farmer",
                        $"{b.StartDate:dd MMM yyyy}"
                    },
                    linkUrl: AppLinks.OwnerRequests("godown", b.Id),
                    dedupeKey: $"reminder:godown:{b.Id}:owner-start:{b.ModificationCount}"));
            }

            return await NotifyManyAsync(requests, ct);
        }

        /// <summary>
        /// R3: Farmer — Equipment, Status == Paid, today < EndDate <= today + 1
        /// </summary>
        private async Task<(int Sent, int Skipped)> RunRule3ReturnDueFarmerAsync(CancellationToken ct, string? onlyUserId)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var query = _equipmentBookings.Query()
                .Include(b => b.Equipment)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Paid && b.EndDate.Date > today && b.EndDate.Date <= tomorrow);

            if (!string.IsNullOrEmpty(onlyUserId))
            {
                query = query.Where(b => b.FarmerId == onlyUserId);
            }

            var bookings = await query.ToListAsync(ct);
            var requests = bookings.Select(b => BuildFarmerRequest(
                userId: b.FarmerId,
                titleKey: "Equipment return due tomorrow",
                messageKey: "Your rental of {0} ends tomorrow ({1}). Please return it on time.",
                args: new object[]
                {
                    b.Equipment?.Name ?? "Equipment",
                    $"{b.EndDate:dd MMM yyyy}"
                },
                linkUrl: AppLinks.FarmerBookings("equipment", b.Id),
                dedupeKey: $"reminder:equipment:{b.Id}:end:{b.ModificationCount}",
                recipientEmail: b.Farmer?.Email)).ToList();

            return await NotifyManyAsync(requests, ct);
        }

        /// <summary>
        /// R4: Farmer — Godown, Status == Paid, today < EndDate <= today + StorageEndingDays
        /// </summary>
        private async Task<(int Sent, int Skipped)> RunRule4StorageEndingFarmerAsync(CancellationToken ct, string? onlyUserId)
        {
            var today = DateTime.Today;
            var maxEndDate = today.AddDays(_options.Value.StorageEndingDays);

            var query = _godownBookings.Query()
                .Include(b => b.Godown)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Paid && b.EndDate.Date > today && b.EndDate.Date <= maxEndDate);

            if (!string.IsNullOrEmpty(onlyUserId))
            {
                query = query.Where(b => b.FarmerId == onlyUserId);
            }

            var bookings = await query.ToListAsync(ct);
            var requests = bookings.Select(b => BuildFarmerRequest(
                userId: b.FarmerId,
                titleKey: "Storage period ending soon",
                messageKey: "Your storage of {0} t at {1} ends on {2}. Extend the booking or arrange collection.",
                args: new object[]
                {
                    b.StorageTons.ToString("0.#"),
                    b.Godown?.Name ?? "Godown",
                    $"{b.EndDate:dd MMM yyyy}"
                },
                linkUrl: AppLinks.FarmerBookings("godown", b.Id),
                dedupeKey: $"reminder:godown:{b.Id}:ending",
                recipientEmail: b.Farmer?.Email)).ToList();

            return await NotifyManyAsync(requests, ct);
        }

        /// <summary>
        /// R5: Farmer — Equipment or Godown, Status == Accepted, AgreedGross > 0, Payment == null || Payment.Status == Failed,
        /// UpdatedOn <= now - UnpaidNudgeHours, StartDate >= today
        /// </summary>
        private async Task<(int Sent, int Skipped)> RunRule5PaymentPendingFarmerAsync(CancellationToken ct, string? onlyUserId)
        {
            var today = DateTime.Today;
            var cutoffUtc = DateTime.UtcNow.AddHours(-_options.Value.UnpaidNudgeHours);
            var cutoffLocal = DateTime.Now.AddHours(-_options.Value.UnpaidNudgeHours);

            var eqQuery = _equipmentBookings.Query()
                .Include(b => b.Equipment)
                .Include(b => b.Farmer)
                .Include(b => b.Payment)
                .Where(b => b.Status == BookingStatus.Accepted
                    && (b.AgreedGross ?? 0m) > 0m
                    && (b.Payment == null || b.Payment.Status == PaymentStatus.Failed)
                    && b.UpdatedOn != null
                    && (b.UpdatedOn <= cutoffUtc || b.UpdatedOn <= cutoffLocal)
                    && b.StartDate.Date >= today);

            var gdQuery = _godownBookings.Query()
                .Include(b => b.Godown)
                .Include(b => b.Farmer)
                .Include(b => b.Payment)
                .Where(b => b.Status == BookingStatus.Accepted
                    && (b.AgreedGross ?? 0m) > 0m
                    && (b.Payment == null || b.Payment.Status == PaymentStatus.Failed)
                    && b.UpdatedOn != null
                    && (b.UpdatedOn <= cutoffUtc || b.UpdatedOn <= cutoffLocal)
                    && b.StartDate.Date >= today);

            if (!string.IsNullOrEmpty(onlyUserId))
            {
                eqQuery = eqQuery.Where(b => b.FarmerId == onlyUserId);
                gdQuery = gdQuery.Where(b => b.FarmerId == onlyUserId);
            }

            var eqBookings = await eqQuery.ToListAsync(ct);
            var gdBookings = await gdQuery.ToListAsync(ct);

            var requests = new List<NotificationRequest>();

            foreach (var b in eqBookings)
            {
                requests.Add(BuildFarmerRequest(
                    userId: b.FarmerId,
                    titleKey: "Payment pending for your booking",
                    messageKey: "Your booking for {0} ({1}) was accepted but is not paid yet. Pay ৳{2} to confirm it.",
                    args: new object[]
                    {
                        b.Equipment?.Name ?? "Equipment",
                        ListingFormat.DateRange(b.StartDate, b.EndDate),
                        $"{(b.AgreedGross ?? 0m):N0}"
                    },
                    linkUrl: AppLinks.FarmerBookings("equipment", b.Id),
                    dedupeKey: $"reminder:equipment:{b.Id}:unpaid",
                    recipientEmail: b.Farmer?.Email));
            }

            foreach (var b in gdBookings)
            {
                requests.Add(BuildFarmerRequest(
                    userId: b.FarmerId,
                    titleKey: "Payment pending for your booking",
                    messageKey: "Your booking for {0} ({1}) was accepted but is not paid yet. Pay ৳{2} to confirm it.",
                    args: new object[]
                    {
                        b.Godown?.Name ?? "Godown",
                        ListingFormat.DateRange(b.StartDate, b.EndDate),
                        $"{(b.AgreedGross ?? 0m):N0}"
                    },
                    linkUrl: AppLinks.FarmerBookings("godown", b.Id),
                    dedupeKey: $"reminder:godown:{b.Id}:unpaid",
                    recipientEmail: b.Farmer?.Email));
            }

            return await NotifyManyAsync(requests, ct);
        }

        /// <summary>
        /// R6: Owner — Equipment or Godown, Status == Paid, EndDate <= today - CompletionOverdueDays
        /// </summary>
        private async Task<(int Sent, int Skipped)> RunRule6OverdueCompletionOwnerAsync(CancellationToken ct, string? onlyUserId)
        {
            var today = DateTime.Today;
            var completionCutoff = today.AddDays(-_options.Value.CompletionOverdueDays);

            var eqQuery = _equipmentBookings.Query()
                .Include(b => b.Equipment)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Paid && b.EndDate.Date <= completionCutoff && b.Equipment != null && !string.IsNullOrEmpty(b.Equipment.OwnerId));

            var gdQuery = _godownBookings.Query()
                .Include(b => b.Godown)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Paid && b.EndDate.Date <= completionCutoff && b.Godown != null && !string.IsNullOrEmpty(b.Godown.OwnerId));

            if (!string.IsNullOrEmpty(onlyUserId))
            {
                eqQuery = eqQuery.Where(b => b.Equipment!.OwnerId == onlyUserId);
                gdQuery = gdQuery.Where(b => b.Godown!.OwnerId == onlyUserId);
            }

            var eqBookings = await eqQuery.ToListAsync(ct);
            var gdBookings = await gdQuery.ToListAsync(ct);

            var requests = new List<NotificationRequest>();

            foreach (var b in eqBookings)
            {
                requests.Add(BuildOwnerRequest(
                    userId: b.Equipment!.OwnerId,
                    titleKey: "Mark booking completed",
                    messageKey: "{0}'s booking of {1} ended on {2}. Mark it completed to release your payout.",
                    args: new object[]
                    {
                        b.Farmer?.FullName ?? "A farmer",
                        b.Equipment?.Name ?? "Equipment",
                        $"{b.EndDate:dd MMM yyyy}"
                    },
                    linkUrl: AppLinks.OwnerRequests("equipment", b.Id),
                    dedupeKey: $"reminder:equipment:{b.Id}:complete"));
            }

            foreach (var b in gdBookings)
            {
                requests.Add(BuildOwnerRequest(
                    userId: b.Godown!.OwnerId,
                    titleKey: "Mark booking completed",
                    messageKey: "{0}'s booking of {1} ended on {2}. Mark it completed to release your payout.",
                    args: new object[]
                    {
                        b.Farmer?.FullName ?? "A farmer",
                        b.Godown?.Name ?? "Godown",
                        $"{b.EndDate:dd MMM yyyy}"
                    },
                    linkUrl: AppLinks.OwnerRequests("godown", b.Id),
                    dedupeKey: $"reminder:godown:{b.Id}:complete"));
            }

            return await NotifyManyAsync(requests, ct);
        }

        /// <summary>
        /// R7: Owner — Equipment or Godown, Status == Pending, RequestedOn <= now - StalePendingHours, StartDate >= today
        /// </summary>
        private async Task<(int Sent, int Skipped)> RunRule7StalePendingOwnerAsync(CancellationToken ct, string? onlyUserId)
        {
            var today = DateTime.Today;
            var cutoffUtc = DateTime.UtcNow.AddHours(-_options.Value.StalePendingHours);
            var cutoffLocal = DateTime.Now.AddHours(-_options.Value.StalePendingHours);

            var eqQuery = _equipmentBookings.Query()
                .Include(b => b.Equipment)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Pending
                    && (b.RequestedOn <= cutoffUtc || b.RequestedOn <= cutoffLocal)
                    && b.StartDate.Date >= today
                    && b.Equipment != null && !string.IsNullOrEmpty(b.Equipment.OwnerId));

            var gdQuery = _godownBookings.Query()
                .Include(b => b.Godown)
                .Include(b => b.Farmer)
                .Where(b => b.Status == BookingStatus.Pending
                    && (b.RequestedOn <= cutoffUtc || b.RequestedOn <= cutoffLocal)
                    && b.StartDate.Date >= today
                    && b.Godown != null && !string.IsNullOrEmpty(b.Godown.OwnerId));

            if (!string.IsNullOrEmpty(onlyUserId))
            {
                eqQuery = eqQuery.Where(b => b.Equipment!.OwnerId == onlyUserId);
                gdQuery = gdQuery.Where(b => b.Godown!.OwnerId == onlyUserId);
            }

            var eqBookings = await eqQuery.ToListAsync(ct);
            var gdBookings = await gdQuery.ToListAsync(ct);

            var requests = new List<NotificationRequest>();

            foreach (var b in eqBookings)
            {
                var elapsedHours = Math.Max(1, (int)Math.Round(Math.Max((DateTime.Now - b.RequestedOn).TotalHours, (DateTime.UtcNow - b.RequestedOn).TotalHours)));
                requests.Add(BuildOwnerRequest(
                    userId: b.Equipment!.OwnerId,
                    titleKey: "A farmer is waiting for your reply",
                    messageKey: "{0} requested {1} ({2}) {3} hours ago. Accept or decline so they can plan.",
                    args: new object[]
                    {
                        b.Farmer?.FullName ?? "A farmer",
                        b.Equipment?.Name ?? "Equipment",
                        ListingFormat.DateRange(b.StartDate, b.EndDate),
                        elapsedHours
                    },
                    linkUrl: AppLinks.OwnerRequests("equipment", b.Id),
                    dedupeKey: $"reminder:equipment:{b.Id}:stale"));
            }

            foreach (var b in gdBookings)
            {
                var elapsedHours = Math.Max(1, (int)Math.Round(Math.Max((DateTime.Now - b.RequestedOn).TotalHours, (DateTime.UtcNow - b.RequestedOn).TotalHours)));
                requests.Add(BuildOwnerRequest(
                    userId: b.Godown!.OwnerId,
                    titleKey: "A farmer is waiting for your reply",
                    messageKey: "{0} requested {1} ({2}) {3} hours ago. Accept or decline so they can plan.",
                    args: new object[]
                    {
                        b.Farmer?.FullName ?? "A farmer",
                        b.Godown?.Name ?? "Godown",
                        ListingFormat.DateRange(b.StartDate, b.EndDate),
                        elapsedHours
                    },
                    linkUrl: AppLinks.OwnerRequests("godown", b.Id),
                    dedupeKey: $"reminder:godown:{b.Id}:stale"));
            }

            return await NotifyManyAsync(requests, ct);
        }

        // ---------------------------------------------------------------- Helpers

        private async Task<(int Sent, int Skipped)> NotifyManyAsync(IEnumerable<NotificationRequest> requests, CancellationToken ct)
        {
            var sent = 0;
            var skipped = 0;

            foreach (var req in requests)
            {
                if (ct.IsCancellationRequested) break;
                var delivered = await _notifications.NotifyAsync(req);
                if (delivered)
                {
                    sent++;
                }
                else
                {
                    skipped++;
                }
            }

            return (sent, skipped);
        }

        private static NotificationRequest BuildFarmerRequest(
            string userId,
            string titleKey,
            string messageKey,
            object[] args,
            string linkUrl,
            string dedupeKey,
            string? recipientEmail)
        {
            return new NotificationRequest
            {
                UserId = userId,
                Type = NotificationTypes.Reminder,
                TitleKey = titleKey,
                MessageKey = messageKey,
                Args = args,
                LinkUrl = linkUrl,
                DedupeKey = dedupeKey,
                SendEmail = true,
                RecipientEmail = recipientEmail
            };
        }

        private static NotificationRequest BuildOwnerRequest(
            string userId,
            string titleKey,
            string messageKey,
            object[] args,
            string linkUrl,
            string dedupeKey)
        {
            return new NotificationRequest
            {
                UserId = userId,
                Type = NotificationTypes.Reminder,
                TitleKey = titleKey,
                MessageKey = messageKey,
                Args = args,
                LinkUrl = linkUrl,
                DedupeKey = dedupeKey,
                SendEmail = false
            };
        }
    }
}
