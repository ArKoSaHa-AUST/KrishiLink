using System;
using System.Collections.Generic;
using System.Linq;
using KrishiLink.DAL;

namespace KrishiLink.BLL.Helpers
{
    /// <summary>
    /// Administrative geography mapping for Bangladesh: 8 divisions and 64 districts, read from the reviewed seed file
    /// <c>App_Data/seed/districts.json</c> (QLT-04) — the single source for every district list, alias and centroid.
    /// </summary>
    public static class BangladeshGeo
    {
        private static readonly ReferenceDataSeed.Geography Seed = ReferenceDataSeed.CurrentGeography;

        public static readonly IReadOnlyList<string> Divisions = Seed.Divisions;

        /// <summary>Every name a district is known by (today's spelling and older ones) to its division.</summary>
        private static readonly Dictionary<string, string> DistrictToDivisionMap = Seed.Districts
            .SelectMany(d => d.Aliases.Prepend(d.Name).Select(name => (name, d.Division)))
            .ToDictionary(x => x.name, x => x.Division, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Pre-2018 spellings kept in the lookup map so old records still resolve, but never offered
        /// in a picker: the canonical name is always shown instead.
        /// </summary>
        private static readonly Dictionary<string, string> LegacySpellings = Seed.Districts
            .SelectMany(d => d.Aliases.Select(alias => (alias, d.Name)))
            .ToDictionary(x => x.alias, x => x.Name, StringComparer.OrdinalIgnoreCase);

        /// <summary>The 64 canonical district names, alphabetically ordered.</summary>
        public static readonly IReadOnlyList<string> AllDistricts = Seed.Districts
            .Select(d => d.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        /// <summary>
        /// Division to its canonical districts, in the order the divisions themselves are listed.
        /// Backs the cascading division/district pickers.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DistrictsByDivision =
            Divisions.ToDictionary(
                division => division,
                division => (IReadOnlyList<string>)Seed.Districts
                    .Where(d => d.Division.Equals(division, StringComparison.OrdinalIgnoreCase))
                    .Select(d => d.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        /// <summary>Centroids under every spelling, for maps and distance search.</summary>
        internal static readonly IReadOnlyDictionary<string, (double Latitude, double Longitude)> DistrictCoordinates = Seed.Districts
            .SelectMany(d => d.Aliases.Prepend(d.Name).Select(name => (name, d.Lat, d.Lng)))
            .ToDictionary(x => x.name, x => (x.Lat, x.Lng), StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the administrative division for a given district name or alias.
        /// </summary>
        public static string GetDivision(string? district)
        {
            if (string.IsNullOrWhiteSpace(district)) return "Dhaka";
            return DistrictToDivisionMap.TryGetValue(district.Trim(), out var division) ? division : "Dhaka";
        }

        /// <summary>
        /// Returns all districts belonging to a specified division.
        /// </summary>
        public static List<string> GetDistrictsForDivision(string? division)
        {
            if (string.IsNullOrWhiteSpace(division) || division.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                return AllDistricts.ToList();
            }

            return DistrictsByDivision.TryGetValue(division.Trim(), out var districts)
                ? districts.ToList()
                : new List<string>();
        }

        /// <summary>
        /// Maps a legacy spelling to its current district name so stored values and pickers agree.
        /// Unknown names are returned unchanged.
        /// </summary>
        public static string? Canonical(string? district)
        {
            if (string.IsNullOrWhiteSpace(district)) return district;
            var trimmed = district.Trim();
            return LegacySpellings.TryGetValue(trimmed, out var current) ? current : trimmed;
        }

        /// <summary>True when the name is one of the 8 divisions.</summary>
        public static bool IsDivision(string? name) =>
            !string.IsNullOrWhiteSpace(name) && Divisions.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);

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
            return suitableDivision.Contains(userDivision, StringComparison.OrdinalIgnoreCase)
                || suitableDivision.Contains(district.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the latitude and longitude coordinates for a given district in Bangladesh.
        /// Defaults to Dhaka coordinates if not found.
        /// </summary>
        public static (double Latitude, double Longitude) GetCoordinates(string? district)
        {
            if (string.IsNullOrWhiteSpace(district)) return (23.8103, 90.4125);
            return DistrictCoordinates.TryGetValue(district.Trim(), out var coords) ? coords : (23.8103, 90.4125);
        }
    }
}
