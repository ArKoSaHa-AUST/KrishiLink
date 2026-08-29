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
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public IActionResult Register(string? role = null)
        {
            // If already signed in, redirect to respective dashboard
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectBasedOnRole(role);
            }

            var validRoles = new[] { "Farmer", "EquipmentOwner", "GodownOwner" };
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
            var validRoles = new[] { "Farmer", "EquipmentOwner", "GodownOwner" };
            if (!validRoles.Contains(model.Role))
            {
                model.Role = "Farmer";
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

            // Ensure Identity Roles exist in the database
            foreach (var roleName in validRoles)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                }
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
                BusinessOrFarmName = (model.Role == "EquipmentOwner" || model.Role == "GodownOwner")
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

                // Redirect to role-specific dashboard
                return RedirectBasedOnRole(model.Role);
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

                return RedirectBasedOnRole(user.UserRole);
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
        public async Task<IActionResult> Profile()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    var model = new UserProfileViewModel
                    {
                        FullName = currentUser.FullName ?? string.Empty,
                        PhoneNumber = currentUser.PhoneNumber ?? string.Empty,
                        Email = currentUser.Email,
                        Location = currentUser.Location ?? string.Empty,
                        BusinessOrFarmName = currentUser.BusinessOrFarmName,
                        Role = currentUser.UserRole ?? "Farmer",
                        MemberSince = currentUser.CreatedAt
                    };
                    return View(model);
                }
            }

            // Fallback for preview / demo
            var previewModel = new UserProfileViewModel
            {
                FullName = "Rahim Uddin",
                PhoneNumber = "01712345678",
                Email = "rahim.uddin@krishilink.com",
                Location = "Dinajpur Sadar, Dinajpur",
                BusinessOrFarmName = "Uddin Agro Farm",
                Role = "Farmer",
                MemberSince = DateTime.UtcNow.AddMonths(-6)
            };

            return View(previewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    return Json(new { success = false, message = string.Join(" ", errors) });
                }
                return View("Profile", model);
            }

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    currentUser.FullName = model.FullName.Trim();
                    currentUser.PhoneNumber = model.PhoneNumber.Trim();
                    currentUser.Email = !string.IsNullOrWhiteSpace(model.Email) ? model.Email.Trim() : currentUser.Email;
                    currentUser.Location = model.Location.Trim();
                    currentUser.BusinessOrFarmName = model.BusinessOrFarmName?.Trim();

                    var result = await _userManager.UpdateAsync(currentUser);
                    if (result.Succeeded)
                    {
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            return Json(new { success = true, message = "Profile updated successfully." });
                        }
                        TempData["SuccessMessage"] = "Profile updated successfully.";
                        return RedirectToAction(nameof(Profile));
                    }

                    var errorDesc = string.Join(" ", result.Errors.Select(e => e.Description));
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, message = errorDesc });
                    }
                    ModelState.AddModelError(string.Empty, errorDesc);
                    return View("Profile", model);
                }
            }

            // Preview mode response
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Profile updated successfully (Preview Mode)." });
            }
            TempData["SuccessMessage"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new { success = false, message = string.Join(" ", errors) });
            }

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    var result = await _userManager.ChangePasswordAsync(currentUser, model.CurrentPassword, model.NewPassword);
                    if (result.Succeeded)
                    {
                        await _signInManager.RefreshSignInAsync(currentUser);
                        return Json(new { success = true, message = "Password updated successfully." });
                    }

                    var errorDesc = string.Join(" ", result.Errors.Select(e => e.Description));
                    return Json(new { success = false, message = errorDesc });
                }
            }

            // Preview mode response
            return Json(new { success = true, message = "Password changed successfully (Preview Mode)." });
        }

        private IActionResult RedirectBasedOnRole(string? role)
        {
            if (string.Equals(role, "EquipmentOwner", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "EquipmentOwner");
            }
            if (string.Equals(role, "GodownOwner", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "GodownOwner");
            }
            // Default Farmer
            return RedirectToAction("Dashboard", "Farmer");
        }
    }
}
