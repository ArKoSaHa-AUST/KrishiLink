using System.Globalization;
using System.Security.Claims;
using KrishiLink.BLL.Helpers;
using KrishiLink.BLL.Services.Ai;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace KrishiLink.Controllers
{
    /// <summary>
    /// JSON endpoints behind the docked assistant panel. Signed-in users only (the global filter). The assistant can read
    /// and prepare proposals; confirming a proposal posts the ordinary booking form to the existing action.
    /// </summary>
    public class AiAgentController : Controller
    {
        private readonly IAgentService _agent;
        private readonly IChatModelClient _chat;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IWebHostEnvironment _environment;

        public AiAgentController(
            IAgentService agent,
            IChatModelClient chat,
            UserManager<ApplicationUser> users,
            IWebHostEnvironment environment)
        {
            _agent = agent;
            _chat = chat;
            _users = users;
            _environment = environment;
        }

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>POST: /AiAgent/Send — one conversational turn.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Agent)]
        public async Task<IActionResult> Send(AgentTurnRequest request)
        {
            var caller = await CallerAsync();
            if (caller is null) return Unauthorized();
            return Json(await _agent.SendAsync(caller, request, HttpContext.RequestAborted));
        }

        /// <summary>GET: /AiAgent/History?conversationId= — the latest conversation when no id is given.</summary>
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        public async Task<IActionResult> History(int? conversationId) =>
            Json(await _agent.GetHistoryAsync(UserId, conversationId));

        /// <summary>GET: /AiAgent/Conversations — recent conversation titles for the panel's switcher.</summary>
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        public async Task<IActionResult> Conversations() =>
            Json(await _agent.ListConversationsAsync(UserId));

        /// <summary>POST: /AiAgent/New — start an empty conversation.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.Write)]
        public async Task<IActionResult> New() =>
            Json(await _agent.StartConversationAsync(UserId));

        /// <summary>GET: /AiAgent/Proposal/{id} — the server-rendered confirm form for one of the caller's proposals.</summary>
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReadJson)]
        public async Task<IActionResult> Proposal(Guid id)
        {
            var model = await _agent.GetProposalAsync(UserId, id);
            return model is null ? NotFound() : PartialView("_AiAgentProposal", model);
        }

        /// <summary>GET: /AiAgent/Smoke?prompt= — Development-only round trip through the provider chain (Groq, then Gemini).</summary>
        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.Agent)]
        public async Task<IActionResult> Smoke(string? prompt)
        {
            if (!_environment.IsDevelopment()) return NotFound();
            var request = new ChatRequest
            {
                MaxTokens = 300,
                Temperature = 0.2,
                Messages = new List<ChatMessage> { ChatMessage.User(string.IsNullOrWhiteSpace(prompt) ? "Reply with the single word: ready" : prompt[..Math.Min(prompt.Length, 500)]) }
            };
            try
            {
                var completion = await _chat.CompleteAsync(request, HttpContext.RequestAborted);
                return Json(new
                {
                    ok = true,
                    provider = completion.Provider,
                    model = request.Model,
                    reply = completion.Choices[0].Message?.Content,
                    promptTokens = completion.Usage?.PromptTokens,
                    completionTokens = completion.Usage?.CompletionTokens
                });
            }
            catch (ChatModelUnavailableException ex)
            {
                return Json(new { ok = false, reason = ex.Reason.ToString() });
            }
        }

        /// <summary>Identity and profile come from the auth cookie and the database — never from anything the model says (A1).</summary>
        private async Task<AgentCaller?> CallerAsync()
        {
            var user = await _users.GetUserAsync(User);
            if (user is null) return null;
            var roles = await _users.GetRolesAsync(user);
            var district = BangladeshGeo.Canonical(user.District ?? OnboardingOptions.GuessDistrict(user.Location));
            return new AgentCaller(
                user.Id,
                roles.ToList(),
                User.HasClaim(AppPolicies.EmailVerifiedClaim, "true"),
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                string.IsNullOrWhiteSpace(district) ? null : district,
                user.Specialization);
        }
    }

    /// <summary>
    /// On the existing booking actions: when the posted form came from an assistant proposal, the proposal must belong to
    /// the caller, match the listing, be unexpired and unused — and is then spent. Forms without a proposal id pass through
    /// untouched. This only adds a restriction; every existing check in the action still runs.
    /// </summary>
    public sealed class AgentProposalGate : IAsyncActionFilter
    {
        private readonly IAgentService _agent;
        private readonly IStringLocalizer<SharedResource> _localizer;
        private readonly string _type;

        public AgentProposalGate(IAgentService agent, IStringLocalizer<SharedResource> localizer, string type)
        {
            _agent = agent;
            _localizer = localizer;
            _type = type;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var request = context.HttpContext.Request;
            var form = request.HasFormContentType ? await request.ReadFormAsync() : null;
            var raw = form?[AgentProposal.FieldName].ToString();
            if (string.IsNullOrEmpty(raw))
            {
                await next();
                return;
            }

            int.TryParse(form!["Id"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var listingId);
            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is not null && Guid.TryParse(raw, out var proposalId) && listingId > 0
                && await _agent.ConsumeProposalAsync(userId, proposalId, _type, listingId))
            {
                await next();
                return;
            }

            if (context.Controller is Controller controller)
                controller.TempData["ErrorMessage"] = _localizer["This assistant proposal has expired or was already used. Ask the assistant for a fresh one, or book on this page."].Value;
            context.Result = new RedirectToActionResult("Details", _type == AgentProposal.GodownType ? "Godown" : "Equipment", new { id = listingId });
        }
    }
}
