namespace KrishiLink.Models.Entities
{
    public class CropRecommendation
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string RecommendedCrops { get; set; } = string.Empty;
    }
}
