using KrishiLink.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace KrishiLink.Controllers
{
    /// <summary>
    /// MVC and authorization options shared by Program.cs and the authorization tests, so the tests check the
    /// configuration that ships. Every action requires a signed-in user unless it is deliberately [AllowAnonymous].
    /// </summary>
    public static class MvcSecurity
    {
        public static void Configure(MvcOptions options)
        {
            options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
        }

        public static void ConfigurePolicies(AuthorizationOptions options)
        {
            options.AddPolicy(AppPolicies.VerifiedEmail, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(AppPolicies.EmailVerifiedClaim, "true"));
        }
    }

    /// <summary>
    /// When the only unmet requirement is a confirmed e-mail, the access-denied page explains that and offers the code
    /// flow instead of a generic refusal. Any other failure (wrong role, anonymous) keeps the default behaviour.
    /// </summary>
    public sealed class VerifiedEmailResultHandler : IAuthorizationMiddlewareResultHandler
    {
        public const string Reason = "email";

        private readonly AuthorizationMiddlewareResultHandler _default = new();

        public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            var failed = authorizeResult.AuthorizationFailure?.FailedRequirements.ToList();
            if (authorizeResult.Forbidden && failed is { Count: > 0 }
                && failed.All(r => r is ClaimsAuthorizationRequirement { ClaimType: AppPolicies.EmailVerifiedClaim }))
            {
                context.Response.Redirect($"/Account/AccessDenied?reason={Reason}");
                return Task.CompletedTask;
            }
            return _default.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}
