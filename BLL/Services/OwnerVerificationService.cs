using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KrishiLink.DAL;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    public interface IOwnerVerificationService
    {
        Task<OwnerVerificationViewModel?> GetVerificationStatusAsync(string userId);
        Task<(bool Success, string? ErrorMessage)> SubmitVerificationAsync(string userId, OwnerVerificationViewModel model);
        Task<bool> ApproveVerificationAsync(string userId, string? adminId = null, string? notes = null);
        Task<bool> RejectVerificationAsync(string userId, string reason, string? adminId = null);
        Task<bool> ResetVerificationAsync(string userId);
        Task<List<OwnerVerificationRequest>> GetPendingVerificationsAsync();
        string MaskNid(string? nidOrEncrypted);
        string DecryptNid(string cipherText);
    }

    public class OwnerVerificationService : IOwnerVerificationService
    {
        private const string UploadFolder = "verifications";
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _files;
        private readonly INotificationService _notifications;
        private readonly IDataProtector _protector;

        public OwnerVerificationService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IFileStorageService files,
            INotificationService notifications,
            IDataProtectionProvider dataProtectionProvider)
        {
            _db = db;
            _userManager = userManager;
            _files = files;
            _notifications = notifications;
            _protector = dataProtectionProvider.CreateProtector("KrishiLink.Nid");
        }

        public string MaskNid(string? nidOrEncrypted)
        {
            if (string.IsNullOrWhiteSpace(nidOrEncrypted)) return string.Empty;
            var raw = nidOrEncrypted.Trim();
            if (raw.StartsWith("CfDJ8") || raw.Length > 30)
            {
                try
                {
                    raw = _protector.Unprotect(raw);
                }
                catch
                {
                    return "***-***-****";
                }
            }
            if (raw.Length <= 4) return raw;
            var last4 = raw[^4..];
            return $"***-***-{last4}";
        }

        public string DecryptNid(string cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText)) return string.Empty;
            if (!cipherText.StartsWith("CfDJ8") && cipherText.Length <= 20) return cipherText;
            try
            {
                return _protector.Unprotect(cipherText);
            }
            catch
            {
                return cipherText;
            }
        }

        public async Task<OwnerVerificationViewModel?> GetVerificationStatusAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return null;

            var latestRequest = await _db.VerificationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.SubmittedAt)
                .FirstOrDefaultAsync();

            var rawOrEncryptedNid = latestRequest?.NidNumber ?? user.NidNumber;
            var masked = MaskNid(rawOrEncryptedNid);
            var last4 = latestRequest?.NidLast4 ?? (masked.Length >= 4 ? masked[^4..] : null);

            return new OwnerVerificationViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                UserRole = user.UserRole,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Email = user.Email,
                BusinessOrFarmName = user.BusinessOrFarmName,
                Location = user.Location,
                NidNumber = masked,
                MaskedNidNumber = masked,
                NidLast4 = last4,
                ExistingNidFrontUrl = user.NidFrontImagePath ?? latestRequest?.NidFrontImagePath,
                ExistingNidBackUrl = user.NidBackImagePath ?? latestRequest?.NidBackImagePath,
                ExistingTradeLicenseUrl = user.TradeLicenseImagePath ?? latestRequest?.TradeLicenseImagePath,
                VerificationStatus = user.VerificationStatus ?? "Unverified",
                IsVerified = user.IsVerified,
                SubmittedAt = user.VerificationSubmittedAt ?? latestRequest?.SubmittedAt,
                ReviewedAt = user.VerificationReviewedAt ?? latestRequest?.ReviewedAt,
                RejectionReason = user.VerificationRejectionReason ?? latestRequest?.RejectionReason,
                AdminNotes = user.VerificationNotes ?? latestRequest?.AdminNotes
            };
        }

        public static (bool IsValid, string? ErrorMessage) ValidateNidFormat(string? nid)
        {
            var cleanNid = nid?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cleanNid) ||
                !(cleanNid.Length == 10 || cleanNid.Length == 13 || cleanNid.Length == 17) ||
                !cleanNid.All(char.IsDigit))
            {
                return (false, "Please provide a valid 10-digit Smart NID or 13/17-digit legacy Bangladeshi NID number.");
            }

            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> SubmitVerificationAsync(string userId, OwnerVerificationViewModel model)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return (false, "User session is invalid.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null)
                return (false, "Owner user record not found.");

            // Guard: Check if a request is already pending
            var hasPending = await _db.VerificationRequests.AnyAsync(r => r.UserId == userId && r.Status == "Pending");
            if (hasPending)
            {
                return (false, "You already have an active verification request under review. Please wait for administrator approval.");
            }

            // Guard: If already verified
            if (user.IsVerified && user.VerificationStatus == "Verified")
            {
                return (false, "Your owner account is already verified.");
            }

            // 1. Validate NID format
            var (isValidNid, nidError) = ValidateNidFormat(model.NidNumber);
            if (!isValidNid)
            {
                return (false, nidError);
            }
            var cleanNid = model.NidNumber!.Trim();
            var last4 = cleanNid.Length >= 4 ? cleanNid[^4..] : cleanNid;
            var protectedNid = _protector.Protect(cleanNid);
            var maskedNid = $"***-***-{last4}";

            var newFront = model.NidFrontImage is { Length: > 0 };
            var newBack = model.NidBackImage is { Length: > 0 };
            var newTrade = model.TradeLicenseImage is { Length: > 0 };
            var uploads = new[] { model.NidFrontImage, model.NidBackImage, model.TradeLicenseImage }
                .OfType<Microsoft.AspNetCore.Http.IFormFile>().Where(file => file.Length > 0).ToList();
            var validationErrors = _files.ValidateFiles(uploads);
            if (validationErrors.Count > 0)
                return (false, string.Join(" ", validationErrors));

            if (!newFront && string.IsNullOrWhiteSpace(user.NidFrontImagePath))
            {
                return (false, "Please upload a clear photo of the front side of your National ID (NID).");
            }

            // Validate the entire submission before any upload; a partial upload cleans itself up.
            var savedPaths = await _files.SavePrivateFilesAsync(uploads, $"{UploadFolder}/{user.Id}");
            var nextPath = 0;
            var frontPath = newFront ? savedPaths[nextPath++] : user.NidFrontImagePath!;
            var backPath = newBack ? savedPaths[nextPath++] : user.NidBackImagePath;
            var tradeLicensePath = newTrade ? savedPaths[nextPath++] : user.TradeLicenseImagePath;
            OwnerVerificationRequest request;
            var commitAttempted = false;
            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                user.NidNumber = maskedNid;
                user.NidFrontImagePath = frontPath;
                user.NidBackImagePath = backPath;
                user.TradeLicenseImagePath = tradeLicensePath;
                user.VerificationStatus = "Pending";
                user.IsVerified = false;
                user.VerificationSubmittedAt = DateTime.UtcNow;
                user.VerificationRejectionReason = null;
                user.VerificationNotes = null;

                var update = await _userManager.UpdateAsync(user);
                if (!update.Succeeded)
                    throw new InvalidOperationException("The verification profile could not be saved.");

                request = new OwnerVerificationRequest
                {
                    UserId = user.Id,
                    NidNumber = protectedNid,
                    NidLast4 = last4,
                    NidFrontImagePath = frontPath,
                    NidBackImagePath = backPath,
                    TradeLicenseImagePath = tradeLicensePath,
                    Status = "Pending",
                    SubmittedAt = DateTime.UtcNow
                };
                _db.VerificationRequests.Add(request);
                await _db.SaveChangesAsync();
                commitAttempted = true;
                await transaction.CommitAsync();
            }
            catch (Exception saveError)
            {
                // A lost COMMIT acknowledgement does not prove rollback. Keep potentially referenced objects.
                if (commitAttempted)
                    throw new InvalidOperationException(
                        "Verification commit outcome is uncertain. Uploaded documents were retained for reconciliation.",
                        saveError);
                try
                {
                    await _files.DeletePrivateFilesAsync(savedPaths, user.Id);
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException("Verification save and document cleanup failed.", saveError, cleanupError);
                }
                throw;
            }

            // Historical requests retain their documents. Notification failures must not remove committed files.
            await _notifications.NotifyAsync(new NotificationRequest
            {
                UserId = user.Id,
                Type = NotificationTypes.Verification,
                TitleKey = "NID Verification Submitted",
                MessageKey = "Your National ID verification request ({0}) has been submitted and is currently under review.",
                Args = new object[] { maskedNid },
                LinkUrl = AppLinks.Verification(),
                DedupeKey = $"verify:{user.Id}:Submitted:{request.SubmittedAt.Ticks}",
                SendEmail = false
            });

            return (true, null);
        }

        public async Task<bool> ApproveVerificationAsync(string userId, string? adminId = null, string? notes = null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return false;

            user.IsVerified = true;
            user.VerificationStatus = "Verified";
            user.VerificationReviewedAt = DateTime.UtcNow;
            user.VerificationNotes = notes ?? "Approved by administrator/system.";
            user.VerificationRejectionReason = null;

            await _userManager.UpdateAsync(user);

            var pendingRequests = await _db.VerificationRequests
                .Where(r => r.UserId == userId && r.Status == "Pending")
                .ToListAsync();

            foreach (var req in pendingRequests)
            {
                req.Status = "Approved";
                req.ReviewedAt = DateTime.UtcNow;
                req.ReviewedByAdminId = adminId ?? "SystemAdmin";
                req.AdminNotes = notes;
            }

            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(new NotificationRequest
            {
                UserId = user.Id,
                Type = NotificationTypes.Verification,
                TitleKey = "Congratulations! Account Verified",
                MessageKey = "Your identity verification has been approved. A 'Verified Owner' trust badge is now displayed on all your listings.",
                LinkUrl = AppLinks.Verification(),
                DedupeKey = $"verify:{user.Id}:Approved:{DateTime.UtcNow.Ticks}",
                SendEmail = true,
                RecipientEmail = user.Email
            });

            return true;
        }

        public async Task<bool> RejectVerificationAsync(string userId, string reason, string? adminId = null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return false;

            user.IsVerified = false;
            user.VerificationStatus = "Rejected";
            user.VerificationReviewedAt = DateTime.UtcNow;
            user.VerificationRejectionReason = reason;

            await _userManager.UpdateAsync(user);

            var pendingRequests = await _db.VerificationRequests
                .Where(r => r.UserId == userId && r.Status == "Pending")
                .ToListAsync();

            foreach (var req in pendingRequests)
            {
                req.Status = "Rejected";
                req.ReviewedAt = DateTime.UtcNow;
                req.ReviewedByAdminId = adminId ?? "SystemAdmin";
                req.RejectionReason = reason;
            }

            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(new NotificationRequest
            {
                UserId = user.Id,
                Type = NotificationTypes.Verification,
                TitleKey = "Verification Request Update",
                MessageKey = "Your identity verification could not be approved. Reason: {0}. Please re-submit clear documents.",
                Args = new object[] { reason },
                LinkUrl = AppLinks.Verification(),
                DedupeKey = $"verify:{user.Id}:Rejected:{DateTime.UtcNow.Ticks}",
                SendEmail = true,
                RecipientEmail = user.Email
            });

            return true;
        }

        public async Task<bool> ResetVerificationAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return false;

            user.IsVerified = false;
            user.VerificationStatus = "Unverified";
            user.NidNumber = null;
            user.NidFrontImagePath = null;
            user.NidBackImagePath = null;
            user.TradeLicenseImagePath = null;
            user.VerificationSubmittedAt = null;
            user.VerificationReviewedAt = null;
            user.VerificationRejectionReason = null;
            user.VerificationNotes = null;

            await _userManager.UpdateAsync(user);
            return true;
        }

        public async Task<List<OwnerVerificationRequest>> GetPendingVerificationsAsync()
        {
            return await _db.VerificationRequests
                .Include(r => r.User)
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.SubmittedAt)
                .ToListAsync();
        }
    }
}
