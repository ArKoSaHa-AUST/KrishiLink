using System.Threading.Tasks;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace KrishiLink.Controllers.Admin
{
    [Authorize(Roles = AppRoles.Admin)]
    [Route("Admin/[controller]")]
    public class VerificationsController : Controller
    {
        private readonly IOwnerVerificationService _verificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<VerificationsController> _logger;

        public VerificationsController(
            IOwnerVerificationService verificationService,
            UserManager<ApplicationUser> userManager,
            ILogger<VerificationsController> logger)
        {
            _verificationService = verificationService;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var pendingRequests = await _verificationService.GetPendingVerificationsAsync();
            return View("~/Views/Admin/Verifications/Index.cshtml", pendingRequests);
        }

        [HttpPost("Approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string id, string? notes)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null) return Challenge();

            _logger.LogInformation("Admin {AdminId} approving verification request for user {UserId}", currentUser.Id, id);

            var success = await _verificationService.ApproveVerificationAsync(id, adminId: currentUser.Id, notes: notes);
            if (success)
            {
                TempData["SuccessMessage"] = "Owner verification approved successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to approve verification.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(string id, string reason)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null) return Challenge();

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["ErrorMessage"] = "Please provide a reason for rejection.";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation("Admin {AdminId} rejecting verification request for user {UserId} with reason {Reason}", currentUser.Id, id, reason);

            var success = await _verificationService.RejectVerificationAsync(id, reason: reason, adminId: currentUser.Id);
            if (success)
            {
                TempData["SuccessMessage"] = "Owner verification request rejected.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to reject verification.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
