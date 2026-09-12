using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = AppRoles.Farmer)]
    public class FarmerController : Controller
    {
        private readonly IBookingService _bookings;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPestAlertService _pestAlertService;
        private readonly IWeatherSuggestionService _weatherSuggestionService;
        private readonly ILoyaltyService _loyalty;
        private readonly ICropCalendarService _cropCalendarService;

        public FarmerController(
            IBookingService bookings,
            UserManager<ApplicationUser> userManager,
            IPestAlertService pestAlertService,
            IWeatherSuggestionService weatherSuggestionService,
            ILoyaltyService loyalty,
            ICropCalendarService cropCalendarService)
        {
            _bookings = bookings;
            _userManager = userManager;
            _pestAlertService = pestAlertService;
            _weatherSuggestionService = weatherSuggestionService;
            _loyalty = loyalty;
            _cropCalendarService = cropCalendarService;
        }

        /// <summary>GET: /Farmer — redirects to Dashboard.</summary>
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        /// <summary>GET: /Farmer/Dashboard — farmer landing page after login.</summary>
        public async Task<IActionResult> Dashboard()
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var model = await _bookings.GetDashboardAsync(farmerId);

            var farmer = await _userManager.GetUserAsync(User);
            model.FarmerName = string.IsNullOrWhiteSpace(farmer?.FullName) ? User.Identity?.Name ?? "Farmer" : farmer.FullName;

            string district = farmer?.District ?? farmer?.Location ?? "Bogra";
            model.WeatherAlert = await _pestAlertService.GetWeatherAlertNoteAsync(district);
            model.WeatherSuggestion = await _weatherSuggestionService.GetProactiveSuggestionForFarmerAsync(farmerId);
            model.LoyaltyWidget = await _loyalty.GetDashboardWidgetAsync(farmerId);
            model.CropCalendarAdvisory = await _cropCalendarService.GetRecommendationForFarmerAsync(farmerId);

            return View(model);
        }
    }
}
