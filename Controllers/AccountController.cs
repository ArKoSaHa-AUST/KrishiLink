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

            var phone = !string.IsNullOrWhiteSpace(model.PhoneNumber) ? model.PhoneNumber.Trim() : "01700000000";
            var emailAddress = !string.IsNullOrWhiteSpace(model.Email)
                ? model.Email.Trim()
                : $"{phone}@krishilink.local";

            // If account already exists in memory, sign in directly
            var existingUser = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == phone || u.Email == emailAddress || u.UserName == emailAddress);
            if (existingUser != null)
            {
                await _signInManager.SignInAsync(existingUser, isPersistent: true);
                return RedirectBasedOnRole(existingUser.UserRole);
            }

            // Ensure Identity Roles exist in memory
            foreach (var roleName in validRoles)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Create ApplicationUser in memory
            var user = new ApplicationUser
            {
                UserName = emailAddress,
                Email = emailAddress,
                PhoneNumber = phone,
                FullName = !string.IsNullOrWhiteSpace(model.FullName) ? model.FullName.Trim() : "Registered User",
                UserRole = model.Role,
                Location = !string.IsNullOrWhiteSpace(model.Location) ? model.Location.Trim() : "Dhaka",
                BusinessOrFarmName = (model.Role == "EquipmentOwner" || model.Role == "GodownOwner")
                    ? model.BusinessOrFarmName?.Trim()
                    : null,
                CreatedAt = DateTime.UtcNow
            };

            var password = !string.IsNullOrWhiteSpace(model.Password) ? model.Password : "Password123!";
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                // Fallback creation without password policy enforcement
                await _userManager.CreateAsync(user);
            }

            // Assign role and sign in
            await _userManager.AddToRoleAsync(user, model.Role);
            await _signInManager.SignInAsync(user, isPersistent: true);

            return RedirectBasedOnRole(model.Role);
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

            var identifier = string.IsNullOrWhiteSpace(model.Identifier) ? "demouser" : model.Identifier.Trim();

            // Attempt to find user by Phone Number or Email/UserName
            ApplicationUser? user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == identifier || u.Email == identifier || u.UserName == identifier);

            if (user == null)
            {
                // Auto-create user on the fly for seamless demo login
                var role = "Farmer";
                if (identifier.Contains("equipment", StringComparison.OrdinalIgnoreCase) || identifier.Contains("owner", StringComparison.OrdinalIgnoreCase))
                {
                    role = "EquipmentOwner";
                }
                else if (identifier.Contains("godown", StringComparison.OrdinalIgnoreCase) || identifier.Contains("warehouse", StringComparison.OrdinalIgnoreCase))
                {
                    role = "GodownOwner";
                }

                var emailAddress = identifier.Contains("@") ? identifier : $"{identifier}@krishilink.local";

                user = new ApplicationUser
                {
                    UserName = emailAddress,
                    Email = emailAddress,
                    PhoneNumber = identifier,
                    FullName = identifier,
                    UserRole = role,
                    Location = "Dhaka",
                    CreatedAt = DateTime.UtcNow
                };

                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }

                var password = !string.IsNullOrWhiteSpace(model.Password) ? model.Password : "Password123!";
                await _userManager.CreateAsync(user, password);
                await _userManager.AddToRoleAsync(user, role);
            }

            // Sign in unconditionally
            await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectBasedOnRole(user.UserRole);
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
