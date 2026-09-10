namespace KrishiLink.Models.Entities
{
    /// <summary>A date the owner has taken an equipment item off the market (maintenance, personal use).</summary>
    public class EquipmentBlockedDate
    {
        public int Id { get; set; }
        public int EquipmentId { get; set; }
        public Equipment? Equipment { get; set; }
        public DateTime Date { get; set; }
    }
}
