using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KrishiLink.BLL.Helpers
{
    /// <summary>
    /// Geographic helper and geocoding dictionary for all 64 districts in Bangladesh.
    /// Provides fallback coordinates, reverse lookup, and external navigation URLs.
    /// </summary>
    public static class GeoLocationHelper
    {
        /// <summary>Default geographic center of Bangladesh (Dhaka centroid).</summary>
        public const double DefaultBangladeshLat = 23.8103;
        public const double DefaultBangladeshLng = 90.4125;

        /// <summary>
        /// Verified central coordinates for all 64 administrative districts of Bangladesh.
        /// Keys include English names and common alternate spellings.
        /// </summary>
        public static readonly Dictionary<string, (double Lat, double Lng)> DistrictCoordinates = new(StringComparer.OrdinalIgnoreCase)
        {
            // Dhaka Division
            ["Dhaka"] = (23.8103, 90.4125),
            ["Gazipur"] = (24.0023, 90.4264),
            ["Kishoreganj"] = (24.4449, 90.7766),
            ["Manikganj"] = (23.8617, 90.0003),
            ["Munshiganj"] = (23.5422, 90.5305),
            ["Narayanganj"] = (23.6238, 90.5000),
            ["Narsingdi"] = (23.9193, 90.7176),
            ["Tangail"] = (24.2513, 89.9167),
            ["Faridpur"] = (23.6071, 89.8429),
            ["Gopalganj"] = (23.0051, 89.8266),
            ["Madaripur"] = (23.1641, 90.1897),
            ["Rajbari"] = (23.7574, 89.6445),
            ["Shariatpur"] = (23.2423, 90.4348),

            // Rajshahi Division
            ["Bogra"] = (24.8465, 89.3777),
            ["Bogura"] = (24.8465, 89.3777),
            ["Joypurhat"] = (25.0968, 89.0227),
            ["Naogaon"] = (24.8103, 88.9416),
            ["Natore"] = (24.4206, 88.9322),
            ["Nawabganj"] = (24.5965, 88.2775),
            ["Chapai Nawabganj"] = (24.5965, 88.2775),
            ["Pabna"] = (24.0064, 89.2372),
            ["Rajshahi"] = (24.3636, 88.6241),
            ["Sirajganj"] = (24.4534, 89.7008),

            // Rangpur Division
            ["Dinajpur"] = (25.6279, 88.6332),
            ["Gaibandha"] = (25.3288, 89.5406),
            ["Kurigram"] = (25.8054, 89.6362),
            ["Lalmonirhat"] = (25.9923, 89.2847),
            ["Nilphamari"] = (25.9318, 88.8560),
            ["Panchagarh"] = (26.3411, 88.5542),
            ["Rangpur"] = (25.7439, 89.2752),
            ["Thakurgaon"] = (26.0337, 88.4617),

            // Chittagong Division
            ["Chittagong"] = (22.3569, 91.7832),
            ["Chattogram"] = (22.3569, 91.7832),
            ["Bandarban"] = (22.1953, 92.2184),
            ["Brahmanbaria"] = (23.9571, 91.1119),
            ["Chandpur"] = (23.2333, 90.6667),
            ["Comilla"] = (23.4607, 91.1809),
            ["Cumilla"] = (23.4607, 91.1809),
            ["Cox's Bazar"] = (21.4272, 92.0058),
            ["Feni"] = (23.0186, 91.3966),
            ["Khagrachhari"] = (23.1193, 91.9847),
            ["Lakshmipur"] = (22.9425, 90.8412),
            ["Noakhali"] = (22.8696, 91.0993),
            ["Rangamati"] = (22.7324, 92.2985),

            // Khulna Division
            ["Khulna"] = (22.8456, 89.5403),
            ["Bagerhat"] = (22.6516, 89.7859),
            ["Chuadanga"] = (23.6402, 88.8418),
            ["Jessore"] = (23.1664, 89.2081),
            ["Jashore"] = (23.1664, 89.2081),
            ["Jhenaidah"] = (23.5448, 89.1539),
            ["Kushtia"] = (23.9013, 89.1205),
            ["Magura"] = (23.4873, 89.4198),
            ["Meherpur"] = (23.7622, 88.6318),
            ["Narail"] = (23.1725, 89.5127),
            ["Satkhira"] = (22.7185, 89.0705),

            // Barisal Division
            ["Barisal"] = (22.7010, 90.3535),
            ["Barishal"] = (22.7010, 90.3535),
            ["Barguna"] = (22.0953, 90.0768),
            ["Bhola"] = (22.6859, 90.6481),
            ["Jhalokati"] = (22.6406, 90.1987),
            ["Patuakhali"] = (22.3596, 90.3298),
            ["Pirojpur"] = (22.5841, 89.9720),

            // Sylhet Division
            ["Sylhet"] = (24.8949, 91.8687),
            ["Habiganj"] = (24.3749, 91.4155),
            ["Moulvibazar"] = (24.4829, 91.7774),
            ["Sunamganj"] = (25.0658, 91.3950),

            // Mymensingh Division
            ["Mymensingh"] = (24.7471, 90.4203),
            ["Jamalpur"] = (24.9375, 89.9378),
            ["Netrokona"] = (24.8709, 90.7279),
            ["Sherpur"] = (25.0205, 90.0153)
        };

        /// <summary>
        /// Attempts to parse or detect district centroid coordinates from a location string (e.g. "Shibganj, Bogra" or "Dinajpur").
        /// Returns central Bangladesh coordinates if no district matches.
        /// </summary>
        public static (double Lat, double Lng) GetDistrictCoordinates(string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return (DefaultBangladeshLat, DefaultBangladeshLng);

            var text = location.Trim();

            // Direct exact match
            if (DistrictCoordinates.TryGetValue(text, out var direct))
                return direct;

            // Search comma-separated parts (e.g., "Shibganj, Bogra")
            var parts = text.Split(new[] { ',', '-', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts.Reverse())
            {
                var clean = part.Trim();
                if (DistrictCoordinates.TryGetValue(clean, out var matched))
                    return matched;
            }

            // Substring search for known district names
            foreach (var kvp in DistrictCoordinates)
            {
                if (text.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return (DefaultBangladeshLat, DefaultBangladeshLng);
        }

        /// <summary>
        /// Formats latitude and longitude coordinates into a human-readable display string (e.g., "24.8465° N, 89.3777° E").
        /// </summary>
        public static string FormatCoordinates(double? lat, double? lng)
        {
            if (!lat.HasValue || !lng.HasValue) return string.Empty;

            var latDir = lat.Value >= 0 ? "N" : "S";
            var lngDir = lng.Value >= 0 ? "E" : "W";

            return string.Format(CultureInfo.InvariantCulture, "{0:F4}° {1}, {2:F4}° {3}",
                Math.Abs(lat.Value), latDir, Math.Abs(lng.Value), lngDir);
        }

        /// <summary>
        /// Generates a direct Google Maps Directions navigation URL.
        /// </summary>
        public static string GetGoogleMapsDirectionsUrl(double? lat, double? lng)
        {
            if (!lat.HasValue || !lng.HasValue) return "https://www.google.com/maps";

            return string.Format(CultureInfo.InvariantCulture,
                "https://www.google.com/maps/dir/?api=1&destination={0:F6},{1:F6}",
                lat.Value, lng.Value);
        }

        /// <summary>
        /// Generates an OpenStreetMap direct view URL.
        /// </summary>
        public static string GetOpenStreetMapUrl(double? lat, double? lng, int zoom = 14)
        {
            if (!lat.HasValue || !lng.HasValue) return "https://www.openstreetmap.org";

            return string.Format(CultureInfo.InvariantCulture,
                "https://www.openstreetmap.org/?mlat={0:F6}&mlon={1:F6}#map={2}/{0:F6}/{1:F6}",
                lat.Value, lng.Value, zoom);
        }
    }
}
