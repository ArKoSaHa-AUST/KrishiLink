using System;
using System.Collections.Generic;
using System.Linq;

namespace KrishiLink.BLL.Helpers
{
    /// <summary>
    /// Administrative geography mapping for Bangladesh: 8 divisions, 64 districts, and regional agro-ecological zones.
    /// </summary>
    public static class BangladeshGeo
    {
        public static readonly IReadOnlyList<string> Divisions = new[]
        {
            "Dhaka", "Chattogram", "Rajshahi", "Khulna", "Barishal", "Sylhet", "Rangpur", "Mymensingh"
        };

        private static readonly Dictionary<string, string> DistrictToDivisionMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // Dhaka Division (13 districts)
            ["Dhaka"] = "Dhaka",
            ["Gazipur"] = "Dhaka",
            ["Kishoreganj"] = "Dhaka",
            ["Manikganj"] = "Dhaka",
            ["Munshiganj"] = "Dhaka",
            ["Narayanganj"] = "Dhaka",
            ["Narsingdi"] = "Dhaka",
            ["Tangail"] = "Dhaka",
            ["Faridpur"] = "Dhaka",
            ["Gopalganj"] = "Dhaka",
            ["Madaripur"] = "Dhaka",
            ["Rajbari"] = "Dhaka",
            ["Shariatpur"] = "Dhaka",

            // Chattogram Division (11 districts)
            ["Chattogram"] = "Chattogram",
            ["Chittagong"] = "Chattogram",
            ["Bandarban"] = "Chattogram",
            ["Brahmanbaria"] = "Chattogram",
            ["Chandpur"] = "Chattogram",
            ["Cumilla"] = "Chattogram",
            ["Comilla"] = "Chattogram",
            ["Cox's Bazar"] = "Chattogram",
            ["Feni"] = "Chattogram",
            ["Khagrachhari"] = "Chattogram",
            ["Lakshmipur"] = "Chattogram",
            ["Noakhali"] = "Chattogram",
            ["Rangamati"] = "Chattogram",

            // Rajshahi Division (8 districts)
            ["Rajshahi"] = "Rajshahi",
            ["Bogura"] = "Rajshahi",
            ["Bogra"] = "Rajshahi",
            ["Joypurhat"] = "Rajshahi",
            ["Naogaon"] = "Rajshahi",
            ["Natore"] = "Rajshahi",
            ["Chapainawabganj"] = "Rajshahi",
            ["Nawabganj"] = "Rajshahi",
            ["Pabna"] = "Rajshahi",
            ["Sirajganj"] = "Rajshahi",

            // Khulna Division (10 districts)
            ["Khulna"] = "Khulna",
            ["Bagerhat"] = "Khulna",
            ["Chuadanga"] = "Khulna",
            ["Jashore"] = "Khulna",
            ["Jessore"] = "Khulna",
            ["Jhenaidah"] = "Khulna",
            ["Kushtia"] = "Khulna",
            ["Magura"] = "Khulna",
            ["Meherpur"] = "Khulna",
            ["Narail"] = "Khulna",
            ["Satkhira"] = "Khulna",

            // Barishal Division (6 districts)
            ["Barishal"] = "Barishal",
            ["Barisal"] = "Barishal",
            ["Barguna"] = "Barishal",
            ["Bhola"] = "Barishal",
            ["Jhalokati"] = "Barishal",
            ["Patuakhali"] = "Barishal",
            ["Pirojpur"] = "Barishal",

            // Sylhet Division (4 districts)
            ["Sylhet"] = "Sylhet",
            ["Habiganj"] = "Sylhet",
            ["Moulvibazar"] = "Sylhet",
            ["Maulvibazar"] = "Sylhet",
            ["Sunamganj"] = "Sylhet",

            // Rangpur Division (8 districts)
            ["Rangpur"] = "Rangpur",
            ["Dinajpur"] = "Rangpur",
            ["Gaibandha"] = "Rangpur",
            ["Kurigram"] = "Rangpur",
            ["Lalmonirhat"] = "Rangpur",
            ["Nilphamari"] = "Rangpur",
            ["Panchagarh"] = "Rangpur",
            ["Thakurgaon"] = "Rangpur",

            // Mymensingh Division (4 districts)
            ["Mymensingh"] = "Mymensingh",
            ["Jamalpur"] = "Mymensingh",
            ["Netrokona"] = "Mymensingh",
            ["Sherpur"] = "Mymensingh"
        };

        /// <summary>
        /// Gets the administrative division for a given district name or alias.
        /// </summary>
        public static string GetDivision(string? district)
        {
            if (string.IsNullOrWhiteSpace(district)) return "Dhaka";
            var trimmed = district.Trim();
            if (DistrictToDivisionMap.TryGetValue(trimmed, out var division))
            {
                return division;
            }
            return "Dhaka";
        }

        /// <summary>
        /// Returns all districts belonging to a specified division.
        /// </summary>
        public static List<string> GetDistrictsForDivision(string? division)
        {
            if (string.IsNullOrWhiteSpace(division) || division.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                return DistrictToDivisionMap.Keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            return DistrictToDivisionMap
                .Where(kvp => kvp.Value.Equals(division.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Checks whether a given district matches a crop's suitable division / region filter.
        /// </summary>
        public static bool IsDistrictSuitable(string? district, string? suitableDivision)
        {
            if (string.IsNullOrWhiteSpace(suitableDivision) || suitableDivision.Equals("All", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.IsNullOrWhiteSpace(district))
                return true;

            var userDivision = GetDivision(district);

            // Check direct division match or substring
            return suitableDivision.Contains(userDivision, StringComparison.OrdinalIgnoreCase)
                || suitableDivision.Contains(district.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static readonly Dictionary<string, (double Latitude, double Longitude)> DistrictCoordinates = new(StringComparer.OrdinalIgnoreCase)
        {
            // Dhaka Division
            ["Dhaka"] = (23.8103, 90.4125),
            ["Gazipur"] = (23.9999, 90.4203),
            ["Kishoreganj"] = (24.4449, 90.7766),
            ["Manikganj"] = (23.8617, 90.0003),
            ["Munshiganj"] = (23.5422, 90.5305),
            ["Narayanganj"] = (23.6238, 90.5000),
            ["Narsingdi"] = (23.9322, 90.7154),
            ["Tangail"] = (24.2513, 89.9167),
            ["Faridpur"] = (23.6071, 89.8429),
            ["Gopalganj"] = (23.0051, 89.8266),
            ["Madaripur"] = (23.1641, 90.1897),
            ["Rajbari"] = (23.7574, 89.6445),
            ["Shariatpur"] = (23.2423, 90.4348),

            // Chattogram Division
            ["Chattogram"] = (22.3569, 91.7832),
            ["Chittagong"] = (22.3569, 91.7832),
            ["Bandarban"] = (22.1953, 92.2184),
            ["Brahmanbaria"] = (23.9571, 91.1119),
            ["Chandpur"] = (23.2333, 90.6667),
            ["Cumilla"] = (23.4607, 91.1809),
            ["Comilla"] = (23.4607, 91.1809),
            ["Cox's Bazar"] = (21.4272, 92.0058),
            ["Feni"] = (23.0159, 91.3976),
            ["Khagrachhari"] = (23.1193, 91.9847),
            ["Lakshmipur"] = (22.9425, 90.8412),
            ["Noakhali"] = (22.8696, 91.0993),
            ["Rangamati"] = (22.6533, 92.1753),

            // Rajshahi Division
            ["Rajshahi"] = (24.3636, 88.6241),
            ["Bogura"] = (24.8465, 89.3770),
            ["Bogra"] = (24.8465, 89.3770),
            ["Joypurhat"] = (25.1015, 89.0277),
            ["Naogaon"] = (24.8103, 88.9416),
            ["Natore"] = (24.4206, 88.9324),
            ["Chapainawabganj"] = (24.5965, 88.2775),
            ["Nawabganj"] = (24.5965, 88.2775),
            ["Pabna"] = (24.0064, 89.2372),
            ["Sirajganj"] = (24.4534, 89.7008),

            // Khulna Division
            ["Khulna"] = (22.8456, 89.5403),
            ["Bagerhat"] = (22.6516, 89.7859),
            ["Chuadanga"] = (23.6402, 88.8418),
            ["Jashore"] = (23.1664, 89.2182),
            ["Jessore"] = (23.1664, 89.2182),
            ["Jhenaidah"] = (23.5448, 89.1539),
            ["Kushtia"] = (23.9013, 89.1205),
            ["Magura"] = (23.4873, 89.4199),
            ["Meherpur"] = (23.7622, 88.6318),
            ["Narail"] = (23.1725, 89.5120),
            ["Satkhira"] = (22.7185, 89.0705),

            // Barishal Division
            ["Barishal"] = (22.7010, 90.3535),
            ["Barisal"] = (22.7010, 90.3535),
            ["Barguna"] = (22.0953, 90.1121),
            ["Bhola"] = (22.6859, 90.6481),
            ["Jhalokati"] = (22.6406, 90.1987),
            ["Patuakhali"] = (22.3596, 90.3299),
            ["Pirojpur"] = (22.5841, 89.9720),

            // Sylhet Division
            ["Sylhet"] = (24.8949, 91.8687),
            ["Habiganj"] = (24.3749, 91.4155),
            ["Moulvibazar"] = (24.4829, 91.7774),
            ["Maulvibazar"] = (24.4829, 91.7774),
            ["Sunamganj"] = (25.0658, 91.3950),

            // Rangpur Division
            ["Rangpur"] = (25.7439, 89.2752),
            ["Dinajpur"] = (25.6217, 88.6354),
            ["Gaibandha"] = (25.3288, 89.5281),
            ["Kurigram"] = (25.8054, 89.6362),
            ["Lalmonirhat"] = (25.9923, 89.2847),
            ["Nilphamari"] = (25.9318, 88.8560),
            ["Panchagarh"] = (26.3411, 88.5542),
            ["Thakurgaon"] = (26.0336, 88.4617),

            // Mymensingh Division
            ["Mymensingh"] = (24.7471, 90.4203),
            ["Jamalpur"] = (24.9375, 89.9378),
            ["Netrokona"] = (24.8709, 90.7279),
            ["Sherpur"] = (25.0205, 90.0153)
        };

        /// <summary>
        /// Returns the latitude and longitude coordinates for a given district in Bangladesh.
        /// Defaults to Dhaka coordinates if not found.
        /// </summary>
        public static (double Latitude, double Longitude) GetCoordinates(string? district)
        {
            if (string.IsNullOrWhiteSpace(district)) return (23.8103, 90.4125);
            var trimmed = district.Trim();
            if (DistrictCoordinates.TryGetValue(trimmed, out var coords))
            {
                return coords;
            }
            return (23.8103, 90.4125);
        }
    }
}

