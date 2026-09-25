namespace KrishiLink.Models.Entities
{
    /// <summary>
    /// One thread with the in-app AI assistant. Kept for continuity and to answer "why did the assistant propose
    /// this"; archived after 90 idle days and deleted after 180 by the retention sweep.
    /// </summary>
    public class AgentConversation
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;

        /// <summary>Short title generated from the first user message.</summary>
        public string Title { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsArchived { get; set; }

        public ApplicationUser? User { get; set; }
        public List<AgentMessage> Messages { get; set; } = new();
    }
}
