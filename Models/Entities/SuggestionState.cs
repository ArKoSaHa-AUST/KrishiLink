namespace KrishiLink.Models.Entities
{
    public enum SuggestionStateKind
    {
        New,
        Snoozed,
        Done
    }

    /// <summary>What a farmer did with one weather nudge, so the same advice does not reappear forever (ADV-08).</summary>
    public class SuggestionState
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;

        /// <summary>Nudge id | district | crop | yyyy-MM — see <c>SuggestionStateService.KeyFor</c>.</summary>
        public string SuggestionKey { get; set; } = string.Empty;

        public SuggestionStateKind State { get; set; } = SuggestionStateKind.New;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser? User { get; set; }
    }
}
