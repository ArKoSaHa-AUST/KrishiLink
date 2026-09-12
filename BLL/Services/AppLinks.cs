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
        public static string Favorites => "/Favorites";
        public static string SavedSearches => "/SavedSearches";
        public static string ReceiptVerification(string receiptNumber) => $"/Verify/Receipt/{receiptNumber}";
        public static string WarehouseReceipt(int lotId) => $"/Bookings/WarehouseReceipt/{lotId}";
        public static string OwnerWarehouseReceipt(int lotId) => $"/GodownOwner/WarehouseReceipt/{lotId}";

        public static string BrowseWith(SavedSearch search)
        {
            var isEq = string.Equals(search.ListingType, ListingTypes.Equipment, StringComparison.OrdinalIgnoreCase);
            var path = isEq ? "/Equipment" : "/Godown";
            var query = new Dictionary<string, string?>();

            if (!string.IsNullOrWhiteSpace(search.SearchTerm))
                query["searchTerm"] = search.SearchTerm;

            if (!string.IsNullOrWhiteSpace(search.Category))
            {
                if (isEq) query["selectedCategories"] = search.Category;
                else query["selectedStorageTypes"] = search.Category;
            }

            if (!string.IsNullOrWhiteSpace(search.District))
                query["district"] = search.District;

            if (search.MaxRate.HasValue && search.MaxRate.Value > 0)
                query["selectedMaxPrice"] = search.MaxRate.Value.ToString("0.##");

            if (!isEq && search.MinCapacityTons.HasValue && search.MinCapacityTons.Value > 0)
                query["selectedMinCapacity"] = search.MinCapacityTons.Value.ToString("0.##");

            if (search.From.HasValue)
            {
                if (isEq) query["startDate"] = search.From.Value.ToString("yyyy-MM-dd");
                else query["availableStartDate"] = search.From.Value.ToString("yyyy-MM-dd");
            }

            if (search.To.HasValue)
            {
                if (isEq) query["endDate"] = search.To.Value.ToString("yyyy-MM-dd");
                else query["availableEndDate"] = search.To.Value.ToString("yyyy-MM-dd");
            }

            return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(path, query);
        }
    }
}
