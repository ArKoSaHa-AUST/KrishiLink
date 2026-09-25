namespace KrishiLink.BLL.Services.Ai
{
    /// <summary>
    /// One OpenAI-compatible chat provider. Only non-secret values live in appsettings.json; keys come from
    /// .env / .env.local or the host environment. Model ids are config because providers retire models often.
    /// </summary>
    public abstract class ChatProviderOptions
    {
        /// <summary>Primary key.</summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Standby keys, used only after a key is rejected (revoked or invalid). They are never cycled to get around a
        /// provider's rate limits, which are metered per organization anyway.
        /// </summary>
        public List<string> BackupApiKeys { get; set; } = new();

        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>Tool-calling model for the reasoning loop.</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>Small, fast model for conversation titles.</summary>
        public string FastModel { get; set; } = string.Empty;

        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>Retries after a 429 or 5xx — used only when no other provider is left to fall back to.</summary>
        public int MaxRetries { get; set; } = 2;

        /// <summary>
        /// Sent only to models whose id starts with <see cref="ReasoningEffortModelPrefix"/>: reasoning models spend
        /// thinking tokens inside max_tokens, and other model families reject the parameter.
        /// </summary>
        public string? ReasoningEffort { get; set; } = "low";
        public string ReasoningEffortModelPrefix { get; set; } = string.Empty;

        /// <summary>Gemini 3 rejects tool calls sent back without a thought_signature; see <see cref="ChatToolCall.ExtraContent"/>.</summary>
        public bool RequiresToolCallSignature { get; set; }

        public IReadOnlyList<string> AllApiKeys =>
            new[] { ApiKey }.Concat(BackupApiKeys)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k!.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

        public virtual bool IsConfigured => AllApiKeys.Count > 0 && !string.IsNullOrWhiteSpace(Model) && !string.IsNullOrWhiteSpace(BaseUrl);

        public string ModelFor(ChatModelTier tier) =>
            tier == ChatModelTier.Fast && !string.IsNullOrWhiteSpace(FastModel) ? FastModel : Model;

        public string? ReasoningEffortFor(string model) =>
            !string.IsNullOrWhiteSpace(ReasoningEffort) && ReasoningEffortModelPrefix.Length > 0
                && model.StartsWith(ReasoningEffortModelPrefix, StringComparison.OrdinalIgnoreCase)
                ? ReasoningEffort
                : null;
    }

    public enum ChatModelTier
    {
        /// <summary>The tool-calling reasoning loop.</summary>
        Main,

        /// <summary>Cheap one-shot jobs such as conversation titles.</summary>
        Fast
    }

    /// <summary>
    /// Groq — the primary provider — plus the assistant's own budgets. Keys: GROQ_API_KEY, GROQ_API_KEY_2, GROQ_API_KEY_3.
    /// </summary>
    public sealed class GroqOptions : ChatProviderOptions
    {
        public const string SectionName = "Groq";

        public GroqOptions()
        {
            BaseUrl = "https://api.groq.com/openai/v1";
            ReasoningEffortModelPrefix = "openai/gpt-oss";
        }

        public int MaxTokens { get; set; } = 1024;

        /// <summary>Low on purpose: the assistant reports facts from tools, and creativity is a defect here.</summary>
        public double Temperature { get; set; } = 0.2;

        public int MaxToolIterations { get; set; } = 5;
        public int MaxHistoryTurns { get; set; } = 12;
        public int MaxHistoryTokens { get; set; } = 6000;
        public int MaxMessageLength { get; set; } = 2000;
        public int DailyTokenBudgetPerUser { get; set; } = 120000;
        public int TurnTimeoutSeconds { get; set; } = 20;
        public int ProposalLifetimeMinutes { get; set; } = 15;

        /// <summary>
        /// How long Groq may take on one call before the turn falls over to Gemini, leaving the fallback time to answer
        /// inside <see cref="TurnTimeoutSeconds"/>. Only applies when a fallback provider is configured.
        /// </summary>
        public int FailoverAfterSeconds { get; set; } = 10;
    }

    /// <summary>
    /// Google Gemini through its OpenAI-compatible endpoint — the fallback when Groq is rate-limited, down or slow.
    /// Keys: GEMINI_API_KEY, GEMINI_API_KEY_2. Set Gemini:Enabled=false to send nothing to Google.
    /// </summary>
    public sealed class GeminiOptions : ChatProviderOptions
    {
        public const string SectionName = "Gemini";

        public GeminiOptions()
        {
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai";
            ReasoningEffortModelPrefix = "gemini";
            RequiresToolCallSignature = true;
            TimeoutSeconds = 20;
        }

        public bool Enabled { get; set; } = true;

        public override bool IsConfigured => Enabled && base.IsConfigured;
    }
}
