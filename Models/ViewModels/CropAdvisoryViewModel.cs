using System.ComponentModel.DataAnnotations;
using KrishiLink.BLL.Services;

namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// The Smart Advisor form and its scored results. Every number shown comes from the seeded crop calendar through
    /// <see cref="CropAdvisorScorer"/>, with its factor breakdown.
    /// </summary>
    public class CropAdvisoryViewModel
    {
        // --- Form inputs (validated here, clamped again in the scorer) ---
        public string? Division { get; set; }

        [Required, StringLength(60)]
        public string District { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string Season { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string SoilType { get; set; } = "Clay Loam";

        [Range(3.5, 9.5)]
        public double? SoilPh { get; set; } = 6.5;

        /// <summary>The farmer has not had the soil tested; the pH slider is ignored.</summary>
        public bool PhUnknown { get; set; }

        /// <summary>Land size as typed, in <see cref="LandUnit"/> (REA-03); converted to decimals for the scorer.</summary>
        [Range(0.01, 100000)]
        public double? LandSize { get; set; } = 50;

        public KrishiLink.Models.Entities.LandUnit LandUnit { get; set; } = KrishiLink.Models.Entities.LandUnit.Decimal;

        public bool HasIrrigation { get; set; } = true;

        // --- Results ---
        public bool HasSubmitted { get; set; }
        public CropAdvisoryResult? Result { get; set; }
        public WeatherNoteItem? WeatherAlert { get; set; }

        public AdvisoryContextViewModel Context { get; set; } = new();

        /// <summary>Signed in as a farmer, so each result can be saved to the dashboard.</summary>
        public bool CanSave { get; set; }

        public CropAdvisoryInput ToInput() =>
            new(District, Season, SoilType, PhUnknown ? null : SoilPh,
                LandSize is { } size ? KrishiLink.BLL.Helpers.UnitFormat.ToDecimals(size, LandUnit) : null, HasIrrigation);
    }

    public enum AdvisoryContextSource
    {
        /// <summary>Chosen on one of the advisory pages this session.</summary>
        Selection,

        /// <summary>From the signed-in user's onboarding profile.</summary>
        Profile,

        /// <summary>Nothing better known: the platform default.</summary>
        Default
    }

    /// <summary>Whose district and crop the advisory pages are showing, rendered as a small "change" chip.</summary>
    public class AdvisoryContextViewModel
    {
        public string District { get; set; } = "Bogura";

        /// <summary>A profile crop ("Rice (Boro)") or an alert crop ("Tomato"); "All" when none.</summary>
        public string Crop { get; set; } = "All";

        public AdvisoryContextSource Source { get; set; } = AdvisoryContextSource.Default;
        public bool IsSignedIn { get; set; }

        /// <summary>Where the page's own district/crop controls are, for the chip's "change" link.</summary>
        public string ChangeAnchor { get; set; } = "#advisoryFilters";

        public bool HasCrop => !string.IsNullOrWhiteSpace(Crop) && !Crop.Equals("All", StringComparison.OrdinalIgnoreCase);
    }

    public class WeatherNoteItem
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string IconClass { get; set; } = "bi-cloud-rain-fill";
        public string BadgeText { get; set; } = "Weather Alert";
    }
}
