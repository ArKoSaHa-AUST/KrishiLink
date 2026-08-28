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
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Profile()
        {
            return View();
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
