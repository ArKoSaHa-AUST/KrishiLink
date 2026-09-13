using System.Security.Claims;
using System.Threading.Tasks;
using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KrishiLink.Controllers
{
    [Authorize(Roles = $"{AppRoles.EquipmentOwner},{AppRoles.GodownOwner},{AppRoles.Admin}")]
    public class FarmerProfileController : Controller
    {
        private readonly IFarmerProfileService _profileService;

        public FarmerProfileController(IFarmerProfileService profileService)
        {
            _profileService = profileService;
        }

        /// <summary>
        /// GET: /FarmerProfile/{id}
        /// Read-only trust profile for owners who have received a booking request from this farmer, or administrators.
        /// </summary>
        [HttpGet]
        [Route("FarmerProfile/{id}")]
        public async Task<IActionResult> Index(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var viewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var viewerIsAdmin = User.IsInRole(AppRoles.Admin);

            var profile = await _profileService.GetAsync(id, viewerId, viewerIsAdmin);
            if (profile == null)
            {
                // Returns 404 both when the farmer doesn't exist and when the viewer is not authorized
                // to prevent user ID enumeration.
                return NotFound();
            }

            return View(profile);
        }
    }
}
