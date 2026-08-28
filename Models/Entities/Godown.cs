namespace KrishiLink.Models.Entities
{
    public class Godown
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double CapacityInTons { get; set; }
        public decimal PricePerTonPerMonth { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public ApplicationUser? Owner { get; set; }
    }
}
