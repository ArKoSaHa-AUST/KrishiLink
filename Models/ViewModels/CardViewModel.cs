namespace KrishiLink.Models.ViewModels
{
    /// <summary>
    /// Strongly-typed view model for reusable equipment, godown, and service listing cards.
    /// </summary>
    public class CardViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public string? Location { get; set; }
        public string? Price { get; set; }
        public string? Status { get; set; }
        public string? DetailUrl { get; set; }
    }
}
