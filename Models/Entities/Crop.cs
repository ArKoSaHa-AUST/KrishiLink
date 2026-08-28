namespace KrishiLink.Models.Entities
{
    public class Crop
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public string SoilType { get; set; } = string.Empty;
    }
}
