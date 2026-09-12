using System;
using KrishiLink.Models.Entities;

namespace KrishiLink.BLL.Services
{
    public static class AppLinks
    {
        public static string OwnerRequests(string? role, int? bookingId = null)
        {
            var baseUrl = string.Equals(role, AppRoles.GodownOwner, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(role, "Godown", StringComparison.OrdinalIgnoreCase)
                ? "/GodownOwner/Requests"
                : "/EquipmentOwner/Requests";

            return bookingId.HasValue ? $"{baseUrl}#request-{bookingId.Value}" : baseUrl;
        }

        public static string FarmerBookings(string? type = null, int? bookingId = null)
        {
            var tab = string.Equals(type, "Godown", StringComparison.OrdinalIgnoreCase) ? "godown" : "equipment";
            var url = $"/Bookings?tab={tab}";
            return bookingId.HasValue ? $"{url}&id={bookingId.Value}" : url;
        }

        public static string FarmerBookingsReview(string type, int bookingId)
        {
            var tab = string.Equals(type, "Godown", StringComparison.OrdinalIgnoreCase) ? "godown" : "equipment";
            return $"/Bookings?tab={tab}&review={type.ToLowerInvariant()}:{bookingId}";
        }

        public static string OwnerPayouts(string? role)
        {
            if (string.Equals(role, AppRoles.GodownOwner, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Godown", StringComparison.OrdinalIgnoreCase))
            {
                return "/GodownOwner/Payouts";
            }
            return "/EquipmentOwner/Payouts";
        }

        public static string EquipmentDetails(int id) => $"/Equipment/Details/{id}";
        public static string EquipmentPricing(int id) => $"/EquipmentOwner/Pricing/{id}";
        public static string GodownDetails(int id) => $"/Godown/Details/{id}";
        public static string BrowseEquipment => "/Equipment";
        public static string BrowseGodowns => "/Godown";
        public static string Verification() => "/Account/Verification";
        public static string Notifications => "/Notifications";
        public static string HarvestPlans => "/HarvestPlan";
        public static string HarvestPlan(int id) => $"/HarvestPlan/Details/{id}";
        public static string FarmerProfile(string farmerId) => $"/FarmerProfile/{farmerId}";
    }
}
