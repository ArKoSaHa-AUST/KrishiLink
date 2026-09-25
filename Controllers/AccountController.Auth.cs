using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KrishiLink.Controllers;

public partial class AccountController
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult VerifyEmail(string purpose = "signup")
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return RedirectBasedOnRole(role);
        }
        TempData["SuccessMessage"] = _localizer["Email verification is not required. You can sign in directly."].Value;
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        TempData["SuccessMessage"] = _localizer["Email verification is not required. You can sign in directly."].Value;
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ResendConfirmation(EmailAddressViewModel model)
    {
        TempData["SuccessMessage"] = _localizer["Email verification is not required. You can sign in directly."].Value;
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword() => View(new EmailAddressViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ForgotPassword(EmailAddressViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try { await _auth.SendRecoveryAsync(model.Email.Trim()); }
        catch (SupabaseAuthException) { }
        TempData["SuccessMessage"] = _localizer["If an account exists, a password recovery code will be emailed. Check your inbox and spam folder."].Value;
        return RedirectToAction(nameof(ResetPassword));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword() => View(new ResetPasswordViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        SupabaseAuthTokens? tokens = null;
        try
        {
            tokens = await _auth.VerifyAsync(model.Email.Trim(), model.Code.Trim(), "recovery");
            var remote = await _auth.GetUserAsync(tokens.AccessToken);
            var user = await _userManager.FindByIdAsync(remote.Id);
            if (user == null || remote.EmailConfirmedAt == null)
                throw new SupabaseAuthException("This recovery request cannot be completed.");
            await _auth.ChangePasswordAsync(tokens.AccessToken, model.NewPassword);
            await InvalidatePasswordSessionsAsync(user, tokens.AccessToken);
            TempData["SuccessMessage"] = _localizer["Password reset. Sign in with your new password."].Value;
            return RedirectToAction(nameof(Login));
        }
        catch (SupabaseAuthException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tokens?.AccessToken)) await RevokeProofSessionAsync(tokens.AccessToken);
        }
    }

    private async Task InvalidatePasswordSessionsAsync(ApplicationUser user, string accessToken)
    {
        await _sessions.RevokeLocalSessionsAsync(user.Id);
        var result = await _userManager.UpdateSecurityStampAsync(user);
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        try { await _auth.LogoutAsync(accessToken, allSessions: true); }
        catch (SupabaseAuthException)
        {
            _logger.LogWarning("Password changed but Supabase global logout failed for {UserId}.", user.Id);
            throw new SupabaseAuthException("Password changed and KrishiLink sessions signed out, but provider logout failed. Sign in with your new password and contact support.");
        }
        if (!result.Succeeded)
            throw new SupabaseAuthException("Password changed, but account session invalidation needs administrator attention.");
    }

    private async Task RevokeProofSessionAsync(string accessToken)
    {
        try { await _auth.LogoutAsync(accessToken); }
        catch (SupabaseAuthException)
        {
            _logger.LogWarning("An authentication proof session could not be revoked.");
        }
    }
}
