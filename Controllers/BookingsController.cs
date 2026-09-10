using System.Security.Claims;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = AppRoles.Farmer)]
    public class BookingsController : Controller
    {
        private readonly IBookingService _bookings;

        public BookingsController(IBookingService bookings)
        {
            _bookings = bookings;
        }

        /// <summary>GET: /Bookings — the farmer's rental and storage booking history.</summary>
        public async Task<IActionResult> Index(
            string tab = "all",
            string status = "all",
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string? searchTerm = null)
        {
            var farmerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return View(await _bookings.GetHistoryAsync(farmerId, tab, status, dateFrom, dateTo, searchTerm));
        }
    }
}
