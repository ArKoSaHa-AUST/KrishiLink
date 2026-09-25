namespace KrishiLink.Models.Entities
{
    public enum AgentMessageRole
    {
        System,
        User,
        Assistant,
        Tool
    }

    /// <summary>
    /// One message of an <see cref="AgentConversation"/>. The system prompt is never stored: it is rebuilt every turn.
    /// Tool rows keep the tool name, the model's arguments and the enveloped result for audit.
    /// </summary>
    public class AgentMessage
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public AgentMessageRole Role { get; set; }
        public string Content { get; set; } = string.Empty;

        public string? ToolName { get; set; }
        public string? ToolArgumentsJson { get; set; }

        /// <summary>Upstream tokens for the whole turn, recorded on the assistant message that closes it.</summary>
        public int? TokensIn { get; set; }
        public int? TokensOut { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>A rental/storage proposal the assistant prepared in this turn (a pre-filled form, never a booking).</summary>
        public string? ProposalJson { get; set; }
        public Guid? ProposalId { get; set; }

        /// <summary>Set when the proposal's form was submitted; a proposal is single-use.</summary>
        public DateTime? ProposalUsedAt { get; set; }

        /// <summary>Listing cards and citations shown under an assistant reply, so they survive a page reload.</summary>
        public string? AttachmentsJson { get; set; }

        public AgentConversation? Conversation { get; set; }
    }
}
