using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using KrishiLink.BLL.Helpers;
using KrishiLink.DAL;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services.Ai
{
    public interface IAgentService
    {
        Task<AgentTurnResponse> SendAsync(AgentCaller caller, AgentTurnRequest request, CancellationToken cancellationToken = default);
        Task<AgentHistoryResponse> GetHistoryAsync(string userId, int? conversationId);
        Task<List<AgentConversationSummary>> ListConversationsAsync(string userId);
        Task<AgentConversationSummary> StartConversationAsync(string userId);
        Task<AgentProposalCardViewModel?> GetProposalAsync(string userId, Guid proposalId);

        /// <summary>
        /// Marks a proposal used when its form is submitted. False when it is unknown, someone else's, expired, already used,
        /// or for a different listing. This only ever adds a restriction to the booking action; it never skips its checks.
        /// </summary>
        Task<bool> ConsumeProposalAsync(string userId, Guid proposalId, string type, int listingId);

        Task<(int Archived, int Deleted)> SweepRetentionAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The orchestration loop: history → Groq → tool calls → Groq → answer or proposal. The model reads through
    /// <see cref="AgentTools"/> and can only ever produce a proposal; it has no write path.
    /// </summary>
    public sealed class AgentService : IAgentService
    {
        public const int ArchiveAfterDays = 90;
        public const int DeleteAfterDays = 180;
        private const int MaxHistoryMessages = 60;

        private static readonly JsonSerializerOptions StoreJson = new(JsonSerializerDefaults.Web);

        private readonly ApplicationDbContext _db;
        private readonly IChatModelClient _chat;
        private readonly AgentTools _tools;
        private readonly GroqOptions _options;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly ILogger<AgentService> _logger;

        public AgentService(
            ApplicationDbContext db,
            IChatModelClient chat,
            AgentTools tools,
            IOptions<GroqOptions> options,
            IStringLocalizer<SharedResource> localizer,
            ILogger<AgentService> logger)
        {
            _db = db;
            _chat = chat;
            _tools = tools;
            _options = options.Value;
            _localizer = localizer;
            _logger = logger;
        }

        // ---------------------------------------------------------------- One turn

        public async Task<AgentTurnResponse> SendAsync(AgentCaller caller, AgentTurnRequest request, CancellationToken cancellationToken = default)
        {
            var text = request.Message?.Trim() ?? string.Empty;
            if (text.Length == 0)
                return Refuse(AgentTurnStatus.Invalid, _localizer["Please type a message."].Value, request.ConversationId);
            if (text.Length > _options.MaxMessageLength)
                return Refuse(AgentTurnStatus.Invalid, _localizer["Messages can be at most {0} characters.", _options.MaxMessageLength].Value, request.ConversationId);
            if (!_chat.IsConfigured)
                return Unavailable(text, request.ConversationId);

            var clock = Stopwatch.StartNew();
            var conversation = await LoadOrCreateAsync(caller.UserId, request.ConversationId);

            if (await TokensUsedTodayAsync(caller.UserId) >= _options.DailyTokenBudgetPerUser)
                return Refuse(AgentTurnStatus.Limited, _localizer["You have reached today's assistant limit. It resets at midnight — you can still search and book normally."].Value,
                    conversation.Id == 0 ? null : conversation.Id, FallbackUrl(text));

            var history = await HistoryAsync(conversation.Id);
            var userMessage = new AgentMessage { Role = AgentMessageRole.User, Content = text };
            conversation.Messages.Add(userMessage);

            var messages = new List<ChatMessage> { ChatMessage.System(AgentPrompt.Build(caller)) };
            messages.AddRange(history);
            messages.Add(ChatMessage.User(text));

            LoopOutcome outcome;
            using var turn = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            turn.CancelAfter(TimeSpan.FromSeconds(_options.TurnTimeoutSeconds));
            try
            {
                outcome = await RunLoopAsync(messages, caller, conversation.Messages.Add, turn.Token);
            }
            catch (Exception ex) when (ex is ChatModelUnavailableException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                _logger.LogWarning("Agent turn failed closed ({Reason}) after {ElapsedMs} ms.",
                    ex is ChatModelUnavailableException g ? g.Reason.ToString() : "TurnTimeout", clock.ElapsedMilliseconds);
                await SaveAsync(conversation);
                return Unavailable(text, conversation.Id);
            }

            if (string.IsNullOrWhiteSpace(conversation.Title))
                conversation.Title = await TitleAsync(text, caller.IsBangla, outcome);

            var reply = outcome.Status == AgentTurnStatus.Ok
                ? outcome.Reply!
                : outcome.Stuck
                    ? _localizer["I'm not sure I understood. Could you tell me a bit more — which listing, which dates, or which district?"].Value
                    : _localizer["That needed too many steps. Could you narrow it down — for example one listing, one district or exact dates?"].Value;

            var response = new AgentTurnResponse
            {
                Status = outcome.Status,
                Reply = reply,
                Listings = outcome.Results.Where(r => r.Result.Ok).SelectMany(r => r.Result.Listings)
                    .GroupBy(l => (l.Type, l.Id)).Select(g => g.First()).Take(AgentTools.MaxSearchRows).ToList(),
                Citations = Citations(outcome.Results)
            };
            var proposal = outcome.Results.Select(r => r.Result.Proposal).LastOrDefault(p => p is not null);
            response.ProposalId = proposal?.Id;

            conversation.Messages.Add(new AgentMessage
            {
                Role = AgentMessageRole.Assistant,
                Content = reply,
                TokensIn = outcome.TokensIn,
                TokensOut = outcome.TokensOut,
                ProposalId = proposal?.Id,
                ProposalJson = proposal is null ? null : JsonSerializer.Serialize(proposal, StoreJson),
                AttachmentsJson = response.Listings.Count + response.Citations.Count == 0
                    ? null
                    : JsonSerializer.Serialize(new Attachments(response.Listings, response.Citations), StoreJson)
            });
            await SaveAsync(conversation);

            response.ConversationId = conversation.Id;
            response.Title = conversation.Title;
            _logger.LogInformation("Agent turn in conversation {ConversationId}: {Status} via {Providers}, {Tools} tool call(s), {TokensIn}+{TokensOut} tokens, {ElapsedMs} ms.",
                conversation.Id, outcome.Status, string.Join("+", outcome.Providers), outcome.Results.Count, outcome.TokensIn, outcome.TokensOut, clock.ElapsedMilliseconds);
            _logger.LogDebug("Agent turn text: {Text}", text);
            return response;
        }

        internal sealed class LoopOutcome
        {
            public string Status { get; set; } = AgentTurnStatus.Stopped;
            public string? Reply { get; set; }
            public bool Stuck { get; set; }
            public int Iterations { get; set; }
            public int TokensIn { get; set; }
            public int TokensOut { get; set; }
            public HashSet<string> Providers { get; } = new(StringComparer.Ordinal);
            public List<(string Tool, AgentToolResult Result)> Results { get; } = new();
        }

        /// <summary>
        /// Steps 4–6 of the loop. Hard stop after <see cref="GroqOptions.MaxToolIterations"/> model calls that still ask for
        /// tools, and a clarifying question when the model repeats an identical call (it is stuck; another round-trip won't help).
        /// </summary>
        internal async Task<LoopOutcome> RunLoopAsync(List<ChatMessage> messages, AgentCaller caller, Action<AgentMessage> record, CancellationToken cancellationToken)
        {
            var definitions = AgentTools.DefinitionsFor(caller);
            var outcome = new LoopOutcome();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var iteration = 0; iteration < _options.MaxToolIterations; iteration++)
            {
                outcome.Iterations = iteration + 1;
                var completion = await _chat.CompleteAsync(new ChatRequest
                {
                    Tier = ChatModelTier.Main,
                    Messages = messages,
                    Tools = definitions,
                    ToolChoice = "auto",
                    Temperature = _options.Temperature,
                    MaxTokens = _options.MaxTokens
                }, cancellationToken);
                outcome.TokensIn += completion.Usage?.PromptTokens ?? 0;
                outcome.TokensOut += completion.Usage?.CompletionTokens ?? 0;
                if (completion.Provider.Length > 0) outcome.Providers.Add(completion.Provider);

                var reply = completion.Choices[0].Message!;
                if (reply.ToolCalls is not { Count: > 0 })
                {
                    if (string.IsNullOrWhiteSpace(reply.Content))
                        throw new ChatModelUnavailableException(ChatModelFailure.Malformed, "The model returned an empty answer.");
                    outcome.Status = AgentTurnStatus.Ok;
                    // KrishiLink only ever deals in Taka; models trained mostly on Indian text sometimes slip into ₹.
                    outcome.Reply = reply.Content.Trim().Replace("₹", "৳");
                    return outcome;
                }

                messages.Add(new ChatMessage { Role = "assistant", Content = reply.Content, ToolCalls = reply.ToolCalls });
                foreach (var call in reply.ToolCalls)
                {
                    var name = call.Function.Name ?? string.Empty;
                    if (!seen.Add($"{name}|{Canonical(call.Function.Arguments)}"))
                    {
                        outcome.Stuck = true;
                        return outcome;
                    }

                    var result = await _tools.ExecuteAsync(name, call.Function.Arguments, caller, cancellationToken);
                    var envelope = AgentTools.Envelope(name, result.Data);
                    record(new AgentMessage
                    {
                        Role = AgentMessageRole.Tool,
                        ToolName = name.Length > 64 ? name[..64] : name,
                        ToolArgumentsJson = call.Function.Arguments is { Length: > 4000 } oversized ? oversized[..4000] : call.Function.Arguments,
                        Content = envelope
                    });
                    messages.Add(ChatMessage.ToolResult(call.Id, envelope));
                    outcome.Results.Add((name, result));
                }
            }
            return outcome;
        }

        private static string Canonical(string? json)
        {
            try
            {
                return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json)?.ToJsonString() ?? string.Empty;
            }
            catch (JsonException)
            {
                return json ?? string.Empty;
            }
        }

        private List<AgentCitation> Citations(IEnumerable<(string Tool, AgentToolResult Result)> results) =>
            results.Where(r => r.Result.Ok && AgentTools.Find(r.Tool) is not null)
                .GroupBy(r => r.Tool)
                .Select(g =>
                {
                    var tool = AgentTools.Find(g.Key)!;
                    return new AgentCitation { Tool = tool.Name, Label = _localizer[tool.CitationLabel].Value, Url = g.Last().Result.CitationUrl };
                })
                .ToList();

        /// <summary>Last N user/assistant messages, newest kept first when trimming to the token budget. Tool rows are not replayed.</summary>
        private async Task<List<ChatMessage>> HistoryAsync(int conversationId)
        {
            if (conversationId == 0) return new List<ChatMessage>();
            var rows = await _db.AgentMessages.AsNoTracking()
                .Where(m => m.ConversationId == conversationId && (m.Role == AgentMessageRole.User || m.Role == AgentMessageRole.Assistant))
                .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                .Take(_options.MaxHistoryTurns)
                .Select(m => new { m.Role, m.Content })
                .ToListAsync();

            var kept = new List<ChatMessage>();
            var budget = _options.MaxHistoryTokens;
            foreach (var row in rows)
            {
                budget -= EstimateTokens(row.Content);
                if (budget < 0) break;
                kept.Add(row.Role == AgentMessageRole.User ? ChatMessage.User(row.Content) : ChatMessage.Assistant(row.Content));
            }
            kept.Reverse();
            return kept;
        }

        /// <summary>Deliberately pessimistic (Bangla script costs more tokens per character than English).</summary>
        internal static int EstimateTokens(string text) => (int)Math.Ceiling(text.Length / 3.0) + 4;

        private async Task<string> TitleAsync(string firstMessage, bool bangla, LoopOutcome outcome)
        {
            var fallback = firstMessage.Length <= 60 ? firstMessage : firstMessage[..60].TrimEnd() + "…";
            try
            {
                // A title is a nicety: if it is slow, the first words of the message will do.
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                var completion = await _chat.CompleteAsync(AgentPrompt.TitleRequest(firstMessage, bangla), timeout.Token);
                outcome.TokensIn += completion.Usage?.PromptTokens ?? 0;
                outcome.TokensOut += completion.Usage?.CompletionTokens ?? 0;
                var title = JsonNode.Parse(completion.Choices[0].Message?.Content ?? "{}")?["title"]?.GetValue<string>();
                title = string.Join(' ', (title ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim('"', '\'', ' ');
                return title.Length is > 0 and <= 120 ? title : fallback;
            }
            catch (Exception ex) when (ex is ChatModelUnavailableException or JsonException or InvalidOperationException or OperationCanceledException)
            {
                return fallback;
            }
        }

        private async Task<AgentConversation> LoadOrCreateAsync(string userId, int? conversationId)
        {
            if (conversationId is { } id)
            {
                // Ownership is part of the lookup (A1): someone else's id behaves exactly like an unknown one.
                var existing = await _db.AgentConversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
                if (existing is not null) return existing;
            }
            var created = new AgentConversation { UserId = userId };
            _db.AgentConversations.Add(created);
            return created;
        }

        private async Task SaveAsync(AgentConversation conversation)
        {
            conversation.UpdatedAt = DateTime.UtcNow;
            conversation.IsArchived = false;
            await _db.SaveChangesAsync();
        }

        private async Task<int> TokensUsedTodayAsync(string userId)
        {
            // Bangladesh has a fixed UTC+6 offset, so the Dhaka day starts at 18:00 UTC the day before.
            var dayStartUtc = DateTime.SpecifyKind(BangladeshClock.Today.AddHours(-6), DateTimeKind.Utc);
            return await _db.AgentMessages
                .Where(m => m.Conversation!.UserId == userId && m.CreatedAt >= dayStartUtc && m.TokensIn != null)
                .SumAsync(m => (m.TokensIn ?? 0) + (m.TokensOut ?? 0));
        }

        private AgentTurnResponse Unavailable(string text, int? conversationId) =>
            Refuse(AgentTurnStatus.Unavailable,
                _localizer["The assistant is unavailable right now — you can still search and book normally."].Value,
                conversationId is 0 ? null : conversationId, FallbackUrl(text));

        private static AgentTurnResponse Refuse(string status, string reply, int? conversationId, string? fallbackUrl = null) =>
            new() { Status = status, Reply = reply, ConversationId = conversationId, FallbackUrl = fallbackUrl };

        /// <summary>The manual page closest to what the user asked for, offered whenever the assistant cannot answer.</summary>
        internal static string FallbackUrl(string text)
        {
            static bool Any(string text, params string[] words) => words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
            if (Any(text, "godown", "storage", "store", "cold", "warehouse", "গুদাম", "সংরক্ষণ", "হিমাগার")) return "/Godown";
            if (Any(text, "plant", "crop", "sow", "weather", "rain", "pest", "disease", "harvest time", "ফসল", "আবহাওয়া", "বৃষ্টি", "রোগ", "পোকা", "বপন"))
                return "/Advisory";
            if (Any(text, "my booking", "booking status", "বুকিং")) return "/Bookings";
            return "/Equipment";
        }

        // ---------------------------------------------------------------- Conversations & history

        public async Task<AgentHistoryResponse> GetHistoryAsync(string userId, int? conversationId)
        {
            var query = _db.AgentConversations.AsNoTracking().Where(c => c.UserId == userId);
            var conversation = conversationId is { } id
                ? await query.FirstOrDefaultAsync(c => c.Id == id)
                : await query.Where(c => !c.IsArchived).OrderByDescending(c => c.UpdatedAt).FirstOrDefaultAsync();
            if (conversation is null) return new AgentHistoryResponse();

            var rows = await _db.AgentMessages.AsNoTracking()
                .Where(m => m.ConversationId == conversation.Id && (m.Role == AgentMessageRole.User || m.Role == AgentMessageRole.Assistant))
                .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                .Take(MaxHistoryMessages)
                .Select(m => new { m.Role, m.Content, m.ProposalId, m.AttachmentsJson })
                .ToListAsync();
            rows.Reverse();

            return new AgentHistoryResponse
            {
                ConversationId = conversation.Id,
                Title = conversation.Title,
                Messages = rows.Select(m =>
                {
                    var attachments = Deserialize<Attachments>(m.AttachmentsJson);
                    return new AgentHistoryMessage
                    {
                        Role = m.Role == AgentMessageRole.User ? "user" : "assistant",
                        Content = m.Content,
                        ProposalId = m.ProposalId,
                        Listings = attachments?.Listings ?? new List<AgentListingCard>(),
                        Citations = attachments?.Citations ?? new List<AgentCitation>()
                    };
                }).ToList()
            };
        }

        public Task<List<AgentConversationSummary>> ListConversationsAsync(string userId) =>
            _db.AgentConversations.AsNoTracking()
                .Where(c => c.UserId == userId && !c.IsArchived && c.Messages.Any())
                .OrderByDescending(c => c.UpdatedAt)
                .Take(15)
                .Select(c => new AgentConversationSummary { Id = c.Id, Title = c.Title, UpdatedAt = c.UpdatedAt })
                .ToListAsync();

        public async Task<AgentConversationSummary> StartConversationAsync(string userId)
        {
            var conversation = new AgentConversation { UserId = userId };
            _db.AgentConversations.Add(conversation);
            await _db.SaveChangesAsync();
            return new AgentConversationSummary { Id = conversation.Id, Title = conversation.Title, UpdatedAt = conversation.UpdatedAt };
        }

        // ---------------------------------------------------------------- Proposals

        public async Task<AgentProposalCardViewModel?> GetProposalAsync(string userId, Guid proposalId)
        {
            var row = await ProposalRowAsync(userId, proposalId);
            var proposal = Deserialize<AgentProposal>(row?.ProposalJson);
            if (row is null || proposal is null) return null;
            return new AgentProposalCardViewModel
            {
                Proposal = proposal,
                IsUsed = row.ProposalUsedAt is not null,
                IsExpired = DateTime.UtcNow > proposal.ExpiresAtUtc
            };
        }

        public async Task<bool> ConsumeProposalAsync(string userId, Guid proposalId, string type, int listingId)
        {
            var row = await ProposalRowAsync(userId, proposalId);
            var proposal = Deserialize<AgentProposal>(row?.ProposalJson);
            if (row is null || proposal is null || row.ProposalUsedAt is not null) return false;
            if (DateTime.UtcNow > proposal.ExpiresAtUtc || proposal.Type != type || proposal.ListingId != listingId) return false;

            // Conditional update: of two simultaneous submits only one can flip ProposalUsedAt.
            var updated = await _db.AgentMessages
                .Where(m => m.Id == row.Id && m.ProposalUsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.ProposalUsedAt, DateTime.UtcNow));
            return updated == 1;
        }

        private sealed record ProposalRow(int Id, string? ProposalJson, DateTime? ProposalUsedAt);

        private Task<ProposalRow?> ProposalRowAsync(string userId, Guid proposalId) =>
            _db.AgentMessages.AsNoTracking()
                .Where(m => m.ProposalId == proposalId && m.Conversation!.UserId == userId)
                .Select(m => new ProposalRow(m.Id, m.ProposalJson, m.ProposalUsedAt))
                .FirstOrDefaultAsync();

        // ---------------------------------------------------------------- Retention

        public async Task<(int Archived, int Deleted)> SweepRetentionAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var deleteBefore = now.AddDays(-DeleteAfterDays);
            var archiveBefore = now.AddDays(-ArchiveAfterDays);
            var deleted = await _db.AgentConversations.Where(c => c.UpdatedAt < deleteBefore).ExecuteDeleteAsync(cancellationToken);
            var archived = await _db.AgentConversations.Where(c => !c.IsArchived && c.UpdatedAt < archiveBefore)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsArchived, true), cancellationToken);
            return (archived, deleted);
        }

        private sealed record Attachments(List<AgentListingCard> Listings, List<AgentCitation> Citations);

        private static T? Deserialize<T>(string? json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<T>(json, StoreJson);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
