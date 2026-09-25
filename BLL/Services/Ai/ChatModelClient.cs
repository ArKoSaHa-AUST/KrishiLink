using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services.Ai
{
    // ---------------------------------------------------------------- OpenAI-compatible wire shapes (Groq and Gemini share them)

    public sealed class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string? Content { get; set; }
        public List<ChatToolCall>? ToolCalls { get; set; }
        public string? ToolCallId { get; set; }

        public static ChatMessage System(string content) => new() { Role = "system", Content = content };
        public static ChatMessage User(string content) => new() { Role = "user", Content = content };
        public static ChatMessage Assistant(string content) => new() { Role = "assistant", Content = content };
        public static ChatMessage ToolResult(string toolCallId, string content) => new() { Role = "tool", ToolCallId = toolCallId, Content = content };
    }

    public sealed class ChatToolCall
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "function";
        public ChatFunctionCall Function { get; set; } = new();

        /// <summary>
        /// Provider-specific data that must be sent back unchanged with the call — Gemini 3 puts its thought_signature
        /// here and rejects a tool-call history without it. Opaque to KrishiLink.
        /// </summary>
        public JsonObject? ExtraContent { get; set; }
    }

    public sealed class ChatFunctionCall
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>JSON text produced by the model — untrusted, parsed defensively by the dispatcher.</summary>
        public string Arguments { get; set; } = "{}";
    }

    public sealed class ChatToolDefinition
    {
        public string Type { get; set; } = "function";
        public ChatFunctionDefinition Function { get; set; } = new();
    }

    public sealed class ChatFunctionDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public JsonObject Parameters { get; set; } = new();
    }

    public sealed class ChatRequest
    {
        /// <summary>Which of the provider's models to use; each provider maps it to its own model id.</summary>
        [JsonIgnore]
        public ChatModelTier Tier { get; set; } = ChatModelTier.Main;

        /// <summary>Filled in by the provider client from its configuration.</summary>
        public string Model { get; set; } = string.Empty;
        public List<ChatMessage> Messages { get; set; } = new();
        public List<ChatToolDefinition>? Tools { get; set; }
        public string? ToolChoice { get; set; }
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public ChatResponseFormat? ResponseFormat { get; set; }

        /// <summary>Reasoning models only; filled in by the provider client.</summary>
        public string? ReasoningEffort { get; set; }
    }

    public sealed class ChatResponseFormat
    {
        public string Type { get; set; } = "json_object";
    }

    public sealed class ChatCompletion
    {
        public List<ChatChoice> Choices { get; set; } = new();
        public ChatUsage? Usage { get; set; }

        /// <summary>Which provider answered ("Groq" or "Gemini"), for the audit log.</summary>
        [JsonIgnore]
        public string Provider { get; set; } = string.Empty;
    }

    public sealed class ChatChoice
    {
        public ChatMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    public sealed class ChatUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
    }

    // ---------------------------------------------------------------- Failure model (A8)

    public enum ChatModelFailure
    {
        NotConfigured,
        Timeout,
        RateLimited,
        Upstream,
        Malformed,
        KeyRejected
    }

    /// <summary>Every way an upstream call can fail, collapsed into one exception the agent turns into a friendly message.</summary>
    public sealed class ChatModelUnavailableException : Exception
    {
        public ChatModelUnavailableException(ChatModelFailure reason, string message, Exception? inner = null) : base(message, inner) => Reason = reason;

        public ChatModelFailure Reason { get; }
    }

    public interface IChatModelClient
    {
        bool IsConfigured { get; }
        Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default);
    }

    // ---------------------------------------------------------------- Shared per-provider state

    /// <summary>
    /// Process-wide health per provider: keys that were rejected (skipped for a while, then retried in case the rejection
    /// was transient) and a short cooldown after a rate limit so every turn does not first wait on the throttled provider.
    /// </summary>
    public sealed class ChatProviderStates
    {
        private readonly ConcurrentDictionary<string, State> _states = new(StringComparer.Ordinal);
        private readonly TimeProvider _time;

        public ChatProviderStates() : this(TimeProvider.System) { }

        public ChatProviderStates(TimeProvider time) => _time = time;

        public static readonly TimeSpan RejectedKeyQuarantine = TimeSpan.FromMinutes(30);

        private sealed class State
        {
            public readonly ConcurrentDictionary<string, DateTimeOffset> RejectedUntil = new(StringComparer.Ordinal);
            public DateTimeOffset CoolingUntil;
        }

        private State For(string provider) => _states.GetOrAdd(provider, _ => new State());

        /// <summary>The first key that is not quarantined, or null when every key has been rejected recently.</summary>
        public string? CurrentKey(string provider, IReadOnlyList<string> keys)
        {
            var now = _time.GetUtcNow();
            var state = For(provider);
            return keys.FirstOrDefault(k => !state.RejectedUntil.TryGetValue(k, out var until) || until <= now);
        }

        public void RejectKey(string provider, string key) => For(provider).RejectedUntil[key] = _time.GetUtcNow() + RejectedKeyQuarantine;

        public bool IsCoolingDown(string provider) => For(provider).CoolingUntil > _time.GetUtcNow();

        public void CoolDown(string provider, TimeSpan duration) => For(provider).CoolingUntil = _time.GetUtcNow() + duration;
    }

    // ---------------------------------------------------------------- One OpenAI-compatible provider

    /// <summary>
    /// Chat completions against one OpenAI-compatible provider. Retries 429/5xx with backoff when asked to, moves to a
    /// standby key only when a key is rejected (401/403), maps timeouts and unusable responses to
    /// <see cref="ChatModelUnavailableException"/>, and never logs prompt text or keys.
    /// </summary>
    public sealed class OpenAiCompatibleClient
    {
        internal static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Rate-limit windows usually refill within seconds and say so in Retry-After; the turn's own wall-clock limit
        // still bounds the total wait.
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(10);

        private readonly HttpClient _http;
        private readonly ChatProviderOptions _options;
        private readonly ChatProviderStates _states;
        private readonly ILogger _logger;

        public OpenAiCompatibleClient(string name, HttpClient http, ChatProviderOptions options, ChatProviderStates states, ILogger logger)
        {
            Name = name;
            _http = http;
            _options = options;
            _states = states;
            _logger = logger;
        }

        public string Name { get; }
        public bool IsConfigured => _options.IsConfigured;

        /// <summary>Replaceable so tests exercise the backoff without waiting.</summary>
        internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

        public async Task<ChatCompletion> CompleteAsync(ChatRequest request, int maxRetries, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
                throw new ChatModelUnavailableException(ChatModelFailure.NotConfigured, $"{Name} is not configured.");

            request.Model = _options.ModelFor(request.Tier);
            request.ReasoningEffort = _options.ReasoningEffortFor(request.Model);
            if (_options.RequiresToolCallSignature) AddMissingSignatures(request.Messages);
            var keys = _options.AllApiKeys;

            for (var attempt = 0; ; attempt++)
            {
                var key = _states.CurrentKey(Name, keys)
                    ?? throw new ChatModelUnavailableException(ChatModelFailure.KeyRejected, $"Every {Name} key was rejected.");

                using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                {
                    Content = JsonContent.Create(request, options: Json)
                };
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

                HttpResponseMessage response;
                try
                {
                    response = await _http.SendAsync(message, cancellationToken);
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new ChatModelUnavailableException(ChatModelFailure.Timeout, $"{Name} did not answer in time.", ex);
                }
                catch (HttpRequestException ex)
                {
                    if (attempt < maxRetries)
                    {
                        await Delay(Backoff(attempt, null), cancellationToken);
                        continue;
                    }
                    throw new ChatModelUnavailableException(ChatModelFailure.Upstream, $"{Name} could not be reached.", ex);
                }

                using (response)
                {
                    var status = (int)response.StatusCode;
                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    {
                        // A revoked or invalid key: park it and continue with the next standby key, if any.
                        _states.RejectKey(Name, key);
                        _logger.LogError("{Provider} rejected API key #{KeyIndex} with {Status}; switching to the next configured key.",
                            Name, IndexOf(keys, key) + 1, status);
                        continue;
                    }
                    if (response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500)
                    {
                        _logger.LogWarning("{Provider} returned {Status} (attempt {Attempt}).", Name, status, attempt + 1);
                        if (attempt < maxRetries)
                        {
                            await Delay(Backoff(attempt, response.Headers.RetryAfter), cancellationToken);
                            continue;
                        }
                        if (response.StatusCode == HttpStatusCode.TooManyRequests)
                            _states.CoolDown(Name, response.Headers.RetryAfter?.Delta is { } wait && wait < TimeSpan.FromMinutes(1) ? wait : TimeSpan.FromSeconds(20));
                        throw new ChatModelUnavailableException(
                            response.StatusCode == HttpStatusCode.TooManyRequests ? ChatModelFailure.RateLimited : ChatModelFailure.Upstream,
                            $"{Name} returned {status}.");
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        // 400/404 usually mean a retired model id; not retried.
                        _logger.LogError("{Provider} rejected the request with {Status}. Check its Model settings.", Name, status);
                        throw new ChatModelUnavailableException(ChatModelFailure.Upstream, $"{Name} returned {status}.");
                    }

                    ChatCompletion? completion;
                    try
                    {
                        completion = await response.Content.ReadFromJsonAsync<ChatCompletion>(Json, cancellationToken);
                    }
                    catch (Exception ex) when (ex is JsonException or NotSupportedException)
                    {
                        throw new ChatModelUnavailableException(ChatModelFailure.Malformed, $"{Name} returned malformed JSON.", ex);
                    }

                    if (completion?.Choices is not { Count: > 0 } || completion.Choices[0].Message is null)
                        throw new ChatModelUnavailableException(ChatModelFailure.Malformed, $"{Name} returned no choices.");
                    completion.Provider = Name;
                    return completion;
                }
            }
        }

        /// <summary>Google's documented stand-in for a tool call Gemini did not generate itself (e.g. one Groq made before failover).</summary>
        internal const string PlaceholderSignature = "skip_thought_signature_validator";

        private static void AddMissingSignatures(IEnumerable<ChatMessage> messages)
        {
            foreach (var call in messages.Where(m => m.ToolCalls is not null).SelectMany(m => m.ToolCalls!))
                call.ExtraContent ??= new JsonObject { ["google"] = new JsonObject { ["thought_signature"] = PlaceholderSignature } };
        }

        private static int IndexOf(IReadOnlyList<string> keys, string key)
        {
            for (var i = 0; i < keys.Count; i++)
                if (keys[i] == key) return i;
            return -1;
        }

        private static TimeSpan Backoff(int attempt, RetryConditionHeaderValue? retryAfter)
        {
            var wait = retryAfter?.Delta ?? TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt));
            return wait > MaxBackoff ? MaxBackoff : wait;
        }
    }

    // ---------------------------------------------------------------- Groq first, Gemini as fallback

    /// <summary>
    /// Tries Groq, then Gemini. A provider that is rate-limited, down, slow, out of valid keys or answering garbage is
    /// skipped for the rest of the call; only the last provider in the chain retries, so a fallback always has time to
    /// answer inside the turn's budget. Both speak the same OpenAI format, so one tool loop can move between them.
    /// </summary>
    public sealed class FailoverChatClient : IChatModelClient
    {
        public const string GroqName = "Groq";
        public const string GeminiName = "Gemini";

        private readonly IReadOnlyList<(OpenAiCompatibleClient Client, ChatProviderOptions Options)> _providers;
        private readonly ChatProviderStates _states;
        private readonly TimeSpan _failoverAfter;
        private readonly ILogger<FailoverChatClient> _logger;

        public FailoverChatClient(
            IHttpClientFactory httpClients,
            IOptions<GroqOptions> groq,
            IOptions<GeminiOptions> gemini,
            ChatProviderStates states,
            ILoggerFactory loggers)
            : this(new[]
            {
                (new OpenAiCompatibleClient(GroqName, httpClients.CreateClient(GroqName), groq.Value, states, loggers.CreateLogger<OpenAiCompatibleClient>()), (ChatProviderOptions)groq.Value),
                (new OpenAiCompatibleClient(GeminiName, httpClients.CreateClient(GeminiName), gemini.Value, states, loggers.CreateLogger<OpenAiCompatibleClient>()), (ChatProviderOptions)gemini.Value)
            }, states, TimeSpan.FromSeconds(Math.Max(3, groq.Value.FailoverAfterSeconds)), loggers.CreateLogger<FailoverChatClient>())
        {
        }

        internal FailoverChatClient(
            IEnumerable<(OpenAiCompatibleClient Client, ChatProviderOptions Options)> providers,
            ChatProviderStates states,
            TimeSpan failoverAfter,
            ILogger<FailoverChatClient> logger)
        {
            _providers = providers.Where(p => p.Client.IsConfigured).ToList();
            _states = states;
            _failoverAfter = failoverAfter;
            _logger = logger;
        }

        public bool IsConfigured => _providers.Count > 0;

        public async Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            if (_providers.Count == 0)
                throw new ChatModelUnavailableException(ChatModelFailure.NotConfigured, "No chat provider is configured.");

            // A provider cooling down after a rate limit is tried last rather than first.
            var ordered = _providers.OrderBy(p => _states.IsCoolingDown(p.Client.Name) ? 1 : 0).ToList();
            ChatModelUnavailableException? last = null;

            for (var i = 0; i < ordered.Count; i++)
            {
                var (client, options) = ordered[i];
                var isLast = i == ordered.Count - 1;
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (!isLast) attempt.CancelAfter(_failoverAfter);

                try
                {
                    return await client.CompleteAsync(request, isLast ? options.MaxRetries : 0, attempt.Token);
                }
                catch (ChatModelUnavailableException ex) when (!isLast)
                {
                    last = ex;
                    _logger.LogWarning("{Provider} unavailable ({Reason}); falling back to {Next}.", client.Name, ex.Reason, ordered[i + 1].Client.Name);
                }
                catch (OperationCanceledException) when (!isLast && !cancellationToken.IsCancellationRequested)
                {
                    last = new ChatModelUnavailableException(ChatModelFailure.Timeout, $"{client.Name} was too slow.");
                    _logger.LogWarning("{Provider} took longer than {Seconds}s; falling back to {Next}.", client.Name, _failoverAfter.TotalSeconds, ordered[i + 1].Client.Name);
                }
            }
            throw last ?? new ChatModelUnavailableException(ChatModelFailure.Upstream, "No provider answered.");
        }
    }
}
