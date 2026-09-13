namespace KrishiLink.Models.Entities
{
    /// <summary>A date the owner has closed a godown to new intake (fumigation, maintenance, etc.).</summary>
    public class GodownBlockedDate
    {
        public int Id { get; set; }
        public int GodownId { get; set; }
        public Godown? Godown { get; set; }
        public DateTime Date { get; set; }
        public string? Reason { get; set; }
    }
}
