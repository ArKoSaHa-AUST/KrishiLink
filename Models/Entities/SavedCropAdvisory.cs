namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// A Smart Advisor result a farmer chose to keep, stored with the inputs that produced it so the dashboard can say
    /// what it was based on and the score can be recomputed later. At most three are kept per farmer; newest wins.
    /// </summary>
    public class SavedCropAdvisory
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int CropCalendarEntryId { get; set; }

        public string Season { get; set; } = string.Empty;
        public string SoilType { get; set; } = string.Empty;
        public double? SoilPh { get; set; }
        public double? LandSizeDecimal { get; set; }
        public bool HasIrrigation { get; set; }
        public string District { get; set; } = string.Empty;

        public int MatchScore { get; set; }

        /// <summary>The factor breakdown shown when it was saved, as JSON.</summary>
        public string FactorsJson { get; set; } = "[]";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser? User { get; set; }
        public CropCalendarEntry? Crop { get; set; }
    }
}
