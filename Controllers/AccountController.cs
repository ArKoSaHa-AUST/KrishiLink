using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using KrishiLink.DAL;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.Controllers
{
    public partial class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SupabaseAuthClient _auth;
        private readonly SupabaseSessionService _sessions;
        private readonly SupabaseAdminBootstrap _adminBootstrap;
        private readonly ApplicationDbContext _db;
        private readonly IOwnerVerificationService _verificationService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SupabaseAuthClient auth,
            SupabaseSessionService sessions,
            SupabaseAdminBootstrap adminBootstrap,
            ApplicationDbContext db,
            IOwnerVerificationService verificationService,
            IWebHostEnvironment env,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _auth = auth;
            _sessions = sessions;
            _adminBootstrap = adminBootstrap;
            _db = db;
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

            var validRoles = new[] { AppRoles.Farmer, AppRoles.EquipmentOwner, AppRoles.GodownOwner };
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
        [ServiceFilter(typeof(SupabaseAuthRateLimitFilter))]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Role != AppRoles.Farmer && model.Role != AppRoles.EquipmentOwner && model.Role != AppRoles.GodownOwner)
            {
                ModelState.AddModelError(nameof(model.Role), "Please select a public account role.");
                return View(model);
            }

            var phone = NormalizePhone(model.PhoneNumber);
            var existingByPhone = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
            if (existingByPhone != null)
            {
                ModelState.AddModelError(nameof(model.PhoneNumber), "An account with this phone number is already registered.");
                return View(model);
            }

            var emailAddress = model.Email!.Trim();

            var existingByEmail = await _userManager.FindByEmailAsync(emailAddress);
            if (existingByEmail != null)
            {
                ModelState.AddModelError(nameof(model.Email), "An account with this email address is already registered.");
                return View(model);
            }

            SupabaseAuthUser remote;
            try
            {
                // Admin create rejects duplicate remote accounts and returns only a newly-created UUID.
                // This makes compensation safe; public /signup may conceal duplicates with a fake user.
                remote = await _auth.CreateUserAsync(emailAddress, model.Password);
            }
            catch (SupabaseAuthException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }

            var user = new ApplicationUser
            {
                Id = remote.Id,
                UserName = emailAddress,
                Email = emailAddress,
                EmailConfirmed = false,
                PhoneNumber = phone,
                FullName = model.FullName.Trim(),
                UserRole = model.Role,
                Location = model.Location.Trim(),
                BusinessOrFarmName = (model.Role == AppRoles.EquipmentOwner || model.Role == AppRoles.GodownOwner)
                    ? model.BusinessOrFarmName?.Trim()
                    : null,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();
                var created = await _userManager.CreateAsync(user);
                if (!created.Succeeded) throw new InvalidOperationException("Profile creation failed.");
                var assigned = await _userManager.AddToRoleAsync(user, model.Role);
                if (!assigned.Succeeded) throw new InvalidOperationException("Role assignment failed.");
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Registration profile creation failed ({ErrorType}).", ex.GetType().Name);
                bool? committed = null;
                try
                {
                    // A lost commit acknowledgement must not make us delete a successfully linked Auth user.
                    committed = await _userManager.Users.AsNoTracking().AnyAsync(u => u.Id == remote.Id);
                }
                catch (Exception)
                {
                    _logger.LogError("Registration commit outcome is unknown for Supabase user {UserId}; reconcile before retrying.", remote.Id);
                }
                if (committed == true)
                {
                    TempData["SuccessMessage"] = "Your account was created. Use Resend code to verify your email.";
                    return RedirectToAction(nameof(VerifyEmail));
                }
                if (committed == false)
                {
                    try { await _auth.DeleteUserAsync(remote.Id); }
                    catch (SupabaseAuthException)
                    {
                        _logger.LogError("Registration compensation failed for Supabase user {UserId}; remove the unlinked Auth account before retrying.", remote.Id);
                    }
                }
                ModelState.AddModelError(string.Empty, "Registration could not be completed. Please retry or contact support.");
                return View(model);
            }

            try
            {
                await _auth.SendConfirmationAsync(emailAddress);
                TempData["SuccessMessage"] = "Account created. Check your email for a verification code.";
            }
            catch (SupabaseAuthException)
            {
                // Keep both records: the user can safely resend without recreating either account.
                TempData["ErrorMessage"] = "Account created, but the verification email could not be sent. Use Resend code.";
            }
            return RedirectToAction(nameof(VerifyEmail));
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
        [ServiceFilter(typeof(SupabaseAuthRateLimitFilter))]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var identifier = model.Identifier.Trim();

            var email = identifier;
            if (!identifier.Contains('@'))
            {
                var phone = NormalizePhone(identifier);
                var byPhone = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
                if (byPhone == null)
                {
                    ModelState.AddModelError(string.Empty, "Unable to sign in. Check your credentials, verify your email, or try again shortly.");
                    return View(model);
                }
                email = byPhone.Email!;
            }

            SupabaseAuthTokens? tokens = null;
            var signedIn = false;
            try
            {
                tokens = await _auth.SignInAsync(email, model.Password);
                var user = await _adminBootstrap.ResolveUserAsync(tokens.AccessToken);
                if (user == null) throw new SupabaseAuthException("Account profile not found. Contact support.");
                await _sessions.SignInAsync(HttpContext, user, tokens, model.RememberMe);
                signedIn = true;
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                return user.OnboardingCompletedAt is null
                    ? RedirectToAction(nameof(Onboarding))
                    : RedirectBasedOnRole(user.UserRole);
            }
            catch (SupabaseAuthException)
            {
                ModelState.AddModelError(string.Empty, "Unable to sign in. Check your credentials, verify your email, or try again shortly.");
            }
            finally
            {
                if (!signedIn && tokens != null) await RevokeProofSessionAsync(tokens.AccessToken);
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            if (!await _sessions.SignOutAsync(HttpContext))
            {
                TempData["ErrorMessage"] = "You are signed out of KrishiLink. The authentication provider was unavailable, so its session could not be revoked.";
                return RedirectToAction(nameof(Login));
            }
            return RedirectToAction("Index", "Home");
        }

        private static string NormalizePhone(string value)
        {
            var phone = value.Trim().TrimStart('+');
            if (phone.StartsWith("880", StringComparison.Ordinal)) phone = phone[2..];
            if (phone.Length == 10 && phone.StartsWith('1')) phone = "0" + phone;
            return phone;
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
        [ServiceFilter(typeof(SupabaseAuthRateLimitFilter))]
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

            var phone = NormalizePhone(model.PhoneNumber);
            if (await _userManager.Users.AnyAsync(u => u.Id != currentUser.Id && u.PhoneNumber == phone))
            {
                if (isAjax) return Json(new { success = false, message = "This phone number is already registered." });
                ModelState.AddModelError(nameof(model.PhoneNumber), "This phone number is already registered.");
                return View("Profile", model);
            }
            var emailChanged = !string.Equals(currentUser.Email, model.Email?.Trim(), StringComparison.OrdinalIgnoreCase);
            if (emailChanged && await _userManager.FindByEmailAsync(model.Email!.Trim()) is { } duplicate && duplicate.Id != currentUser.Id)
            {
                if (isAjax) return Json(new { success = false, message = "This email address is already registered." });
                ModelState.AddModelError(nameof(model.Email), "This email address is already registered.");
                return View("Profile", model);
            }
            currentUser.FullName = model.FullName.Trim();
            currentUser.PhoneNumber = phone;
            currentUser.Location = model.Location.Trim();
            currentUser.BusinessOrFarmName = model.BusinessOrFarmName?.Trim();

            var result = await _userManager.UpdateAsync(currentUser);
            if (result.Succeeded)
            {
                var message = "Profile updated successfully.";
                if (emailChanged)
                {
                    var session = _sessions.Current(User);
                    if (session == null) return Challenge();
                    try
                    {
                        await _auth.ChangeEmailAsync(session.Tokens.AccessToken, model.Email!.Trim());
                        message += " Confirm the codes sent to your old and new email addresses using Verify email. Your sign-in email stays unchanged until confirmation.";
                    }
                    catch (SupabaseAuthException ex)
                    {
                        if (isAjax) return Json(new { success = false, message = "Other profile changes were saved. Email change failed: " + ex.Message });
                        TempData["ErrorMessage"] = "Other profile changes were saved. Email change failed: " + ex.Message;
                        return RedirectToAction(nameof(Profile));
                    }
                }
                if (isAjax)
                {
                    return Json(new { success = true, message, email = currentUser.Email });
                }
                TempData["SuccessMessage"] = message;
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
        [ServiceFilter(typeof(SupabaseAuthRateLimitFilter))]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null) return Challenge();

            SupabaseAuthTokens? proof = null;
            try
            {
                proof = await _auth.SignInAsync(currentUser.Email!, model.CurrentPassword);
                var remote = await _auth.GetUserAsync(proof.AccessToken);
                if (remote.Id != currentUser.Id) throw new SupabaseAuthException("Account verification failed.");
                await _auth.ChangePasswordAsync(proof.AccessToken, model.NewPassword);
                await InvalidatePasswordSessionsAsync(currentUser, proof.AccessToken);
                return Json(new { success = true, message = "Password updated. All KrishiLink sessions have been signed out; please log in with your new password.", redirectUrl = Url.Action(nameof(Login)) });
            }
            catch (SupabaseAuthException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            finally
            {
                if (proof != null) await RevokeProofSessionAsync(proof.AccessToken);
            }
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
        /// Streams identity documents from the private Supabase bucket.
        /// Only accessible to the document owner or an Admin.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> VerificationDocument(
            [FromServices] IFileStorageService files, string kind, string? userId = null)
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

            var document = await files.ReadPrivateFileAsync(relativePath, targetUserId, HttpContext.RequestAborted);
            if (document is null) return NotFound();
            Response.Headers["Cache-Control"] = "private, no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(document.Content, document.ContentType);
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
            if (string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Home");
            }
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
