using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KrishiLink.DAL;
using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
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
    }

    public class OwnerVerificationService : IOwnerVerificationService
    {
        private const string UploadFolder = "verifications";
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _files;
        private readonly INotificationService _notifications;

        public OwnerVerificationService(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IFileStorageService files,
            INotificationService notifications)
        {
            _db = db;
            _userManager = userManager;
            _files = files;
            _notifications = notifications;
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

            return new OwnerVerificationViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                UserRole = user.UserRole,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Email = user.Email,
                BusinessOrFarmName = user.BusinessOrFarmName,
                Location = user.Location,
                NidNumber = user.NidNumber ?? latestRequest?.NidNumber ?? string.Empty,
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

            // 1. Validate NID format
            var (isValidNid, nidError) = ValidateNidFormat(model.NidNumber);
            if (!isValidNid)
            {
                return (false, nidError);
            }
            var cleanNid = model.NidNumber!.Trim();

            // 2. Validate Front Image
            string? frontPath = user.NidFrontImagePath;
            if (model.NidFrontImage is not null && model.NidFrontImage.Length > 0)
            {
                var validationErrors = _files.ValidateFiles(new[] { model.NidFrontImage });
                if (validationErrors.Any())
                    return (false, string.Join(" ", validationErrors));

                var savedFront = await _files.SaveImagesAsync(new[] { model.NidFrontImage }, UploadFolder);
                if (savedFront.Any())
                {
                    frontPath = savedFront.First();
                }
            }

            if (string.IsNullOrWhiteSpace(frontPath))
            {
                return (false, "Please upload a clear photo of the front side of your National ID (NID).");
            }

            // 3. Validate Back Image (optional/recommended)
            string? backPath = user.NidBackImagePath;
            if (model.NidBackImage is not null && model.NidBackImage.Length > 0)
            {
                var validationErrors = _files.ValidateFiles(new[] { model.NidBackImage });
                if (validationErrors.Any())
                    return (false, string.Join(" ", validationErrors));

                var savedBack = await _files.SaveImagesAsync(new[] { model.NidBackImage }, UploadFolder);
                if (savedBack.Any())
                {
                    backPath = savedBack.First();
                }
            }

            // 4. Validate Trade License (optional for business owners)
            string? tradeLicensePath = user.TradeLicenseImagePath;
            if (model.TradeLicenseImage is not null && model.TradeLicenseImage.Length > 0)
            {
                var validationErrors = _files.ValidateFiles(new[] { model.TradeLicenseImage });
                if (validationErrors.Any())
                    return (false, string.Join(" ", validationErrors));

                var savedTrade = await _files.SaveImagesAsync(new[] { model.TradeLicenseImage }, UploadFolder);
                if (savedTrade.Any())
                {
                    tradeLicensePath = savedTrade.First();
                }
            }

            // 5. Update ApplicationUser
            user.NidNumber = cleanNid;
            user.NidFrontImagePath = frontPath;
            user.NidBackImagePath = backPath;
            user.TradeLicenseImagePath = tradeLicensePath;
            user.VerificationStatus = "Pending";
            user.IsVerified = false;
            user.VerificationSubmittedAt = DateTime.UtcNow;
            user.VerificationRejectionReason = null;
            user.VerificationNotes = null;

            await _userManager.UpdateAsync(user);

            // 6. Record Verification Request in audit log
            var request = new OwnerVerificationRequest
            {
                UserId = user.Id,
                NidNumber = cleanNid,
                NidFrontImagePath = frontPath,
                NidBackImagePath = backPath,
                TradeLicenseImagePath = tradeLicensePath,
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow
            };

            _db.VerificationRequests.Add(request);
            await _db.SaveChangesAsync();

            // 7. Send user in-app notification
            await _notifications.CreateNotificationAsync(
                userId: user.Id,
                title: "NID Verification Submitted",
                message: $"Your National ID verification request ({cleanNid}) has been submitted and is currently under review.",
                linkUrl: "/Account/Verification",
                type: "Verification"
            );

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

            await _notifications.CreateNotificationAsync(
                userId: user.Id,
                title: "Congratulations! Account Verified",
                message: "Your identity verification has been approved. A 'Verified Owner' trust badge is now displayed on all your listings.",
                linkUrl: "/Account/Verification",
                type: "Verification"
            );

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

            await _notifications.CreateNotificationAsync(
                userId: user.Id,
                title: "Verification Request Update",
                message: $"Your identity verification could not be approved. Reason: {reason}. Please re-submit clear documents.",
                linkUrl: "/Account/Verification",
                type: "Verification"
            );

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
