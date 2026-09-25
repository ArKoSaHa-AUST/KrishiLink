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
        /// Verified central coordinates for all 64 administrative districts of Bangladesh, keyed by today's name and every
        /// older spelling. Read from App_Data/seed/districts.json (QLT-04) — the same table BangladeshGeo uses.
        /// </summary>
        public static readonly Dictionary<string, (double Lat, double Lng)> DistrictCoordinates =
            BangladeshGeo.DistrictCoordinates.ToDictionary(pair => pair.Key, pair => (pair.Value.Latitude, pair.Value.Longitude), StringComparer.OrdinalIgnoreCase);

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
