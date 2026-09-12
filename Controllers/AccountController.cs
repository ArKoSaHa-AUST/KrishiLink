using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;

namespace KrishiLink.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IOwnerVerificationService _verificationService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IOwnerVerificationService verificationService,
            IWebHostEnvironment env,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _verificationService = verificationService;
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Register(string? role = null)
        {
            // If already signed in, redirect to respective dashboard
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectBasedOnRole(role);
            }

            var validRoles = AppRoles.All;
            var selectedRole = !string.IsNullOrEmpty(role) && validRoles.Contains(role, StringComparer.OrdinalIgnoreCase)
                ? validRoles.First(r => r.Equals(role, StringComparison.OrdinalIgnoreCase))
                : "Farmer";

            var model = new RegisterViewModel
            {
                Role = selectedRole
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Normalise role string
            if (!AppRoles.All.Contains(model.Role))
            {
                model.Role = AppRoles.Farmer;
            }

            // Check if phone already registered
            var existingByPhone = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == model.PhoneNumber);
            if (existingByPhone != null)
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "An account with this phone number is already registered.");
                return View(model);
            }

            // Check if email already registered if provided
            var emailAddress = !string.IsNullOrWhiteSpace(model.Email)
                ? model.Email.Trim()
                : $"{model.PhoneNumber.Trim()}@krishilink.local";

            var existingByEmail = await _userManager.FindByEmailAsync(emailAddress);
            if (existingByEmail != null)
            {
                ModelState.AddModelError(nameof(model.Email), "An account with this email address is already registered.");
                return View(model);
            }

            // Create ApplicationUser
            var user = new ApplicationUser
            {
                UserName = emailAddress,
                Email = emailAddress,
                PhoneNumber = model.PhoneNumber.Trim(),
                FullName = model.FullName.Trim(),
                UserRole = model.Role,
                Location = model.Location.Trim(),
                BusinessOrFarmName = (model.Role == AppRoles.EquipmentOwner || model.Role == AppRoles.GodownOwner)
                    ? model.BusinessOrFarmName?.Trim()
                    : null,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // Assign role
                await _userManager.AddToRoleAsync(user, model.Role);

                // Sign in user
                await _signInManager.SignInAsync(user, isPersistent: true);

                // Walk the new user through the short setup before their dashboard
                return RedirectToAction(nameof(Onboarding));
            }

            // Append Identity errors to ModelState
            foreach (var error in result.Errors)
            {
                if (error.Code.Contains("Password", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(model.Password), error.Description);
                }
                else if (error.Code.Contains("Email", StringComparison.OrdinalIgnoreCase) || error.Code.Contains("UserName", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(model.Email), error.Description);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                return RedirectBasedOnRole(currentUser?.UserRole);
            }

            var model = new LoginViewModel
            {
                ReturnUrl = returnUrl
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var identifier = model.Identifier.Trim();

            // Attempt to find user by Phone Number or Email/UserName
            ApplicationUser? user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == identifier);
            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(identifier) ?? await _userManager.FindByNameAsync(identifier);
            }

            if (user == null)
            {
                ModelState.AddModelError(nameof(model.Identifier), "Incorrect phone number or password.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                return user.OnboardingCompletedAt is null
                    ? RedirectToAction(nameof(Onboarding))
                    : RedirectBasedOnRole(user.UserRole);
            }

            ModelState.AddModelError(nameof(model.Password), "Incorrect phone number or password.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null) return Challenge();

            var model = new UserProfileViewModel
            {
                FullName = currentUser.FullName,
                PhoneNumber = currentUser.PhoneNumber ?? string.Empty,
                Email = currentUser.Email,
                Location = currentUser.Location ?? string.Empty,
                BusinessOrFarmName = currentUser.BusinessOrFarmName,
                Role = string.IsNullOrEmpty(currentUser.UserRole) ? AppRoles.Farmer : currentUser.UserRole,
                District = currentUser.District,
                Specialization = currentUser.Specialization,
                OnboardingComplete = currentUser.OnboardingCompletedAt is not null,
                IsVerified = currentUser.IsVerified,
                VerificationStatus = currentUser.VerificationStatus ?? "Unverified",
                NidNumber = currentUser.NidNumber,
                MemberSince = currentUser.CreatedAt
            };
            return View(model);
        }

        /// <summary>
        /// GET: /Account/Onboarding — asks only for the profile details still missing (district, main crop / listing type).
        /// The role was chosen at registration and is not re-asked. <paramref name="edit"/> shows every field for later changes.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Onboarding(bool edit = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();

            var model = BuildOnboarding(user, edit);
            if (!model.AskDistrict && !model.AskSpecialization)
            {
                // Nothing left to ask — mark the setup done and move on
                user.OnboardingCompletedAt ??= DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                return RedirectBasedOnRole(user.UserRole);
            }
            return View(model);
        }

        /// <summary>POST: /Account/Onboarding — saves whichever fields were asked for and marks the setup complete.</summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Onboarding(OnboardingViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Challenge();

            // Fields shown on the form (posted back as hidden flags) plus anything still missing on the profile
            var view = BuildOnboarding(user, edit: false);
            view.AskDistrict |= model.AskDistrict;
            view.AskSpecialization |= model.AskSpecialization;
            view.District = model.District;
            view.Specialization = model.Specialization;

            if (view.AskDistrict && !OnboardingOptions.Districts.Contains(model.District ?? string.Empty))
                ModelState.AddModelError(nameof(model.District), "Please select your district from the list.");
            if (view.AskSpecialization && !view.SpecializationOptions.Contains(model.Specialization ?? string.Empty))
                ModelState.AddModelError(nameof(model.Specialization), "Please pick one of the listed options.");
            if (!ModelState.IsValid) return View(view);

            if (view.AskDistrict) user.District = model.District;
            if (view.AskSpecialization) user.Specialization = model.Specialization;
            var firstTime = user.OnboardingCompletedAt is null;
            user.OnboardingCompletedAt ??= DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = firstTime
                ? $"Welcome to KrishiLink, {user.FullName}! Your profile is set up."
                : "Profile details updated.";
            return firstTime ? RedirectBasedOnRole(user.UserRole) : RedirectToAction(nameof(Profile));
        }

        private static OnboardingViewModel BuildOnboarding(ApplicationUser user, bool edit) => new()
        {
            FullName = user.FullName,
            Role = string.IsNullOrEmpty(user.UserRole) ? AppRoles.Farmer : user.UserRole,
            AskDistrict = edit || string.IsNullOrEmpty(user.District),
            AskSpecialization = edit || string.IsNullOrEmpty(user.Specialization),
            District = user.District ?? OnboardingOptions.GuessDistrict(user.Location),
            Specialization = user.Specialization
        };

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfileViewModel model)
        {
            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (!ModelState.IsValid)
            {
                if (isAjax)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return Json(new { success = false, message = string.Join(" ", errors) });
                }
                return View("Profile", model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null) return Challenge();

            currentUser.FullName = model.FullName.Trim();
            currentUser.PhoneNumber = model.PhoneNumber.Trim();
            currentUser.Email = !string.IsNullOrWhiteSpace(model.Email) ? model.Email.Trim() : currentUser.Email;
            currentUser.Location = model.Location.Trim();
            currentUser.BusinessOrFarmName = model.BusinessOrFarmName?.Trim();

            var result = await _userManager.UpdateAsync(currentUser);
            if (result.Succeeded)
            {
                if (isAjax)
                {
                    return Json(new { success = true, message = "Profile updated successfully." });
                }
                TempData["SuccessMessage"] = "Profile updated successfully.";
                return RedirectToAction(nameof(Profile));
            }

            var errorDesc = string.Join(" ", result.Errors.Select(e => e.Description));
            if (isAjax)
            {
                return Json(new { success = false, message = errorDesc });
            }
            ModelState.AddModelError(string.Empty, errorDesc);
            return View("Profile", model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null) return Challenge();

            var result = await _userManager.ChangePasswordAsync(currentUser, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(currentUser);
                return Json(new { success = true, message = "Password updated successfully." });
            }

            return Json(new { success = false, message = string.Join(" ", result.Errors.Select(e => e.Description)) });
        }

        // ==========================================
        // OWNER IDENTITY VERIFICATION (PHASE 7)
        // ==========================================

        [HttpGet]
        [Authorize(Roles = $"{AppRoles.EquipmentOwner},{AppRoles.GodownOwner}")]
        public async Task<IActionResult> Verification()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null) return Challenge();

            var model = await _verificationService.GetVerificationStatusAsync(currentUser.Id);
            if (model is null) return NotFound();

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = $"{AppRoles.EquipmentOwner},{AppRoles.GodownOwner}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verification(OwnerVerificationViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null) return Challenge();

            var (success, error) = await _verificationService.SubmitVerificationAsync(currentUser.Id, model);
            if (!success)
            {
                TempData["ErrorMessage"] = error;
                var currentModel = await _verificationService.GetVerificationStatusAsync(currentUser.Id) ?? model;
                return View(currentModel);
            }

            TempData["SuccessMessage"] = "Your National ID verification documents have been submitted successfully and are now pending review.";
            return RedirectToAction(nameof(Verification));
        }

        /// <summary>
        /// Serves identity verification documents securely from private storage (App_Data).
        /// Only accessible to the document owner or an Admin.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> VerificationDocument(string kind, string? userId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null) return Challenge();

            var isAdmin = await _userManager.IsInRoleAsync(currentUser, AppRoles.Admin);
            var targetUserId = (isAdmin && !string.IsNullOrWhiteSpace(userId)) ? userId : currentUser.Id;

            if (!isAdmin && !string.IsNullOrWhiteSpace(userId) && !string.Equals(userId, currentUser.Id, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var status = await _verificationService.GetVerificationStatusAsync(targetUserId);
            if (status is null) return NotFound();

            string? relativePath = kind?.ToLowerInvariant() switch
            {
                "front" => status.ExistingNidFrontUrl,
                "back" => status.ExistingNidBackUrl,
                "trade" => status.ExistingTradeLicenseUrl,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(relativePath)) return NotFound();

            var cleanRelative = relativePath.TrimStart('~', '/');
            if (cleanRelative.StartsWith("App_Data/", StringComparison.OrdinalIgnoreCase))
            {
                cleanRelative = cleanRelative.Substring("App_Data/".Length);
            }
            else if (cleanRelative.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback for any legacy file in wwwroot
                var legacyPath = Path.Combine(_env.WebRootPath, cleanRelative.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(legacyPath))
                {
                    Response.Headers["Cache-Control"] = "private, no-store";
                    var legacyMime = GetMimeType(legacyPath);
                    return PhysicalFile(legacyPath, legacyMime);
                }
            }

            var fullPath = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data", cleanRelative.Replace('/', Path.DirectorySeparatorChar)));
            var appDataDir = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "App_Data"));

            if (!fullPath.StartsWith(appDataDir, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            Response.Headers["Cache-Control"] = "private, no-store";
            var mimeType = GetMimeType(fullPath);
            return PhysicalFile(fullPath, mimeType);
        }

        private static string GetMimeType(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Development-only helper to simulate admin verification decisions on the current user.
        /// Returns 404 in production environments and strictly ignores external user IDs.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = $"{AppRoles.EquipmentOwner},{AppRoles.GodownOwner}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DevSimulateVerificationDecision(VerificationReviewSubmitModel model)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null) return Challenge();

            string decision = model.Decision?.ToLowerInvariant() ?? "approve";
            _logger.LogWarning("DevSimulateVerificationDecision executed by user {UserId} with decision {Decision}", currentUser.Id, decision);

            if (decision == "approve")
            {
                await _verificationService.ApproveVerificationAsync(currentUser.Id, adminId: currentUser.Id, notes: model.Notes ?? "Demo instant verification approval.");
                TempData["SuccessMessage"] = "Verification successfully APPROVED! The 'Verified Owner' trust badge is now active on all your listings.";
            }
            else if (decision == "reject")
            {
                await _verificationService.RejectVerificationAsync(currentUser.Id, reason: model.Reason ?? "Document photo is unclear or blurred.", adminId: currentUser.Id);
                TempData["ErrorMessage"] = "Verification status set to REJECTED. Reason recorded.";
            }
            else if (decision == "reset")
            {
                await _verificationService.ResetVerificationAsync(currentUser.Id);
                TempData["SuccessMessage"] = "Verification status RESET to Unverified.";
            }

            return RedirectToAction(nameof(Verification));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectBasedOnRole(string? role)
        {
            if (string.Equals(role, AppRoles.EquipmentOwner, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "EquipmentOwner");
            }
            if (string.Equals(role, AppRoles.GodownOwner, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "GodownOwner");
            }
            // Default Farmer
            return RedirectToAction("Dashboard", "Farmer");
        }
    }
}
