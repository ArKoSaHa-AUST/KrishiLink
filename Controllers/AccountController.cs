using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;

namespace KrishiLink.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
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
