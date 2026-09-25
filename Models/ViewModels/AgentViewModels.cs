using System.ComponentModel.DataAnnotations;

namespace KrishiLink.Models.ViewModels
{
    /// <summary>POST /AiAgent/Send.</summary>
    public class AgentTurnRequest
    {
        public int? ConversationId { get; set; }

        [Required]
        public string Message { get; set; } = string.Empty;
    }

    public static class AgentTurnStatus
    {
        public const string Ok = "ok";
        /// <summary>Message empty or over the length cap.</summary>
        public const string Invalid = "invalid";
        /// <summary>Daily token budget used up.</summary>
        public const string Limited = "limited";
        /// <summary>Groq unreachable, over quota, timed out or returned something unusable (A8).</summary>
        public const string Unavailable = "unavailable";
        /// <summary>The tool loop hit its iteration cap or repeated itself; the reply asks the user to narrow it down.</summary>
        public const string Stopped = "stopped";
    }

    public class AgentTurnResponse
    {
        public string Status { get; set; } = AgentTurnStatus.Ok;
        public int? ConversationId { get; set; }
        public string? Title { get; set; }
        public string Reply { get; set; } = string.Empty;

        /// <summary>Listings returned by search tools this turn, rendered as cards under the reply.</summary>
        public List<AgentListingCard> Listings { get; set; } = new();

        /// <summary>At most one confirmable proposal; the panel fetches its server-rendered form by id.</summary>
        public Guid? ProposalId { get; set; }

        /// <summary>Which platform data the reply was grounded in.</summary>
        public List<AgentCitation> Citations { get; set; } = new();

        /// <summary>The equivalent manual page, offered whenever the assistant cannot help (A8).</summary>
        public string? FallbackUrl { get; set; }
    }

    public class AgentCitation
    {
        public string Tool { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Url { get; set; }
    }

    public class AgentListingCard
    {
        public string Type { get; set; } = string.Empty; // "equipment" | "godown"
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string? District { get; set; }
        public string RateText { get; set; } = string.Empty;
        public double Rating { get; set; }
        public bool IsVerifiedOwner { get; set; }
        public string? ImageUrl { get; set; }
        public string DetailUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// A pre-filled rental or storage request. It executes nothing: it is rendered as an ordinary form that posts to the
    /// existing booking action, which re-validates everything. Proposals expire and are single-use.
    /// </summary>
    public class AgentProposal
    {
        public const string FieldName = "agentProposalId";
        public const string EquipmentType = "equipment";
        public const string GodownType = "godown";

        /// <summary>The only actions a proposal may post to — the existing, fully validated request actions.</summary>
        public static readonly IReadOnlyDictionary<string, string> FormActions = new Dictionary<string, string>
        {
            [EquipmentType] = "/Equipment/SubmitRequest",
            [GodownType] = "/Godown/SubmitBooking"
        };

        public Guid Id { get; set; }
        public string Type { get; set; } = EquipmentType;
        public int ListingId { get; set; }
        public string ListingName { get; set; } = string.Empty;
        public string DetailUrl { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Days { get; set; }

        /// <summary>Units for equipment, tons for storage.</summary>
        public double Quantity { get; set; }
        public string? CropType { get; set; }
        public string? Note { get; set; }
        public decimal QuotedGross { get; set; }
        public string? PricingNote { get; set; }
        public string Currency { get; set; } = "BDT";
        public string FormAction { get; set; } = string.Empty;

        /// <summary>Form field name/value pairs, named exactly as the target action binds them.</summary>
        public Dictionary<string, string> Fields { get; set; } = new();
        public DateTime ExpiresAtUtc { get; set; }
    }

    public class AgentProposalCardViewModel
    {
        public AgentProposal Proposal { get; set; } = new();
        public bool IsExpired { get; set; }
        public bool IsUsed { get; set; }
        public bool CanSubmit => !IsExpired && !IsUsed && AgentProposal.FormActions.Values.Contains(Proposal.FormAction);
    }

    public class AgentConversationSummary
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    public class AgentHistoryMessage
    {
        public string Role { get; set; } = string.Empty; // "user" | "assistant"
        public string Content { get; set; } = string.Empty;
        public List<AgentListingCard> Listings { get; set; } = new();
        public List<AgentCitation> Citations { get; set; } = new();
        public Guid? ProposalId { get; set; }
    }

    public class AgentHistoryResponse
    {
        public int? ConversationId { get; set; }
        public string? Title { get; set; }
        public List<AgentHistoryMessage> Messages { get; set; } = new();
    }
}
